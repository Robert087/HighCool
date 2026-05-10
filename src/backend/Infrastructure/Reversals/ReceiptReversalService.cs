using ERP.Application.Reversals;
using ERP.Application.Statements;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Domain.Shortages;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Reversals;

public sealed class ReceiptReversalService(
    AppDbContext dbContext,
    ISupplierStatementPostingService statementPostingService) : IReceiptReversalService
{
    public async Task<DocumentReversalDto?> ReverseAsync(Guid receiptId, ReverseDocumentRequest request, string actor, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var receipt = await dbContext.PurchaseReceipts
            .AsSplitQuery()
            .Include(entity => entity.Supplier)
            .Include(entity => entity.Lines.OrderBy(line => line.LineNo))
                .ThenInclude(line => line.Item)
            .Include(entity => entity.Lines)
                .ThenInclude(line => line.Uom)
            .SingleOrDefaultAsync(entity => entity.Id == receiptId, cancellationToken);

        if (receipt is null)
        {
            return null;
        }

        EnsurePostedAndNotReversed(receipt.Status, receipt.ReversalDocumentId, "purchase receipt");
        await ValidateDependenciesAsync(receipt.Id, cancellationToken);

        var reversal = await DocumentReversalSupport.CreateAsync(
            dbContext,
            BusinessDocumentType.PurchaseReceipt,
            receipt.Id,
            request,
            actor,
            cancellationToken);

        await CreateStockReversalEntriesAsync(receipt, reversal, actor, cancellationToken);
        await statementPostingService.CreatePurchaseReceiptReversalEntriesAsync(receipt, reversal, actor, cancellationToken);
        await CancelShortagesAsync(receipt.Id, actor, cancellationToken);

        receipt.ReversalDocumentId = reversal.Id;
        receipt.ReversedAt = reversal.ReversalDate;
        receipt.UpdatedBy = actor;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return DocumentReversalSupport.ToDto(reversal);
    }

    private async Task ValidateDependenciesAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        var hasPostedReturns = await dbContext.PurchaseReturns.AnyAsync(
            entity => entity.ReferenceReceiptId == receiptId &&
                      entity.Status == DocumentStatus.Posted &&
                      entity.ReversalDocumentId == null,
            cancellationToken);

        if (hasPostedReturns)
        {
            throw new InvalidOperationException("Purchase receipt reversal is blocked because active purchase returns already exist.");
        }

        var hasActivePayments = await dbContext.PaymentAllocations.AnyAsync(
            entity => entity.TargetDocType == Domain.Payments.PaymentTargetDocumentType.PurchaseReceipt &&
                      entity.TargetDocId == receiptId &&
                      entity.Payment!.Status == DocumentStatus.Posted &&
                      entity.Payment.ReversalDocumentId == null,
            cancellationToken);

        if (hasActivePayments)
        {
            throw new InvalidOperationException("Purchase receipt reversal is blocked because active supplier payment allocations exist.");
        }

        var shortageIds = await dbContext.ShortageLedgerEntries
            .Where(entity => entity.PurchaseReceiptId == receiptId && entity.Status != ShortageEntryStatus.Canceled)
            .Select(entity => entity.Id)
            .ToArrayAsync(cancellationToken);

        if (shortageIds.Length == 0)
        {
            return;
        }

        var hasActiveResolutions = await dbContext.ShortageResolutionAllocations.AnyAsync(
            entity => shortageIds.Contains(entity.ShortageLedgerId) &&
                      entity.Resolution!.Status == DocumentStatus.Posted &&
                      entity.Resolution.ReversalDocumentId == null,
            cancellationToken);

        if (hasActiveResolutions)
        {
            throw new InvalidOperationException("Purchase receipt reversal is blocked because active shortage resolutions exist for its shortages.");
        }
    }

    private async Task CreateStockReversalEntriesAsync(
        Domain.Purchasing.PurchaseReceipt receipt,
        Domain.Reversals.DocumentReversal reversal,
        string actor,
        CancellationToken cancellationToken)
    {
        var runningBalances = new Dictionary<(Guid ItemId, Guid WarehouseId), decimal>();

        foreach (var line in receipt.Lines.OrderBy(entity => entity.LineNo))
        {
            var key = (line.ItemId, receipt.WarehouseId);
            if (!runningBalances.TryGetValue(key, out var runningBalance))
            {
                runningBalance = await dbContext.StockLedgerEntries
                    .Where(entity => entity.ItemId == line.ItemId && entity.WarehouseId == receipt.WarehouseId)
                    .OrderByDescending(entity => entity.TransactionDate)
                    .ThenByDescending(entity => entity.CreatedAt)
                    .ThenByDescending(entity => entity.Id)
                    .Select(entity => entity.RunningBalanceQty)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            var stockEntry = await dbContext.StockLedgerEntries
                .AsNoTracking()
                .SingleAsync(
                    entity => entity.SourceDocType == SourceDocumentType.PurchaseReceipt &&
                              entity.SourceDocId == receipt.Id &&
                              entity.SourceLineId == line.Id,
                    cancellationToken);

            runningBalance -= stockEntry.BaseQty;
            runningBalances[key] = runningBalance;

            dbContext.StockLedgerEntries.Add(new StockLedgerEntry
            {
                ItemId = line.ItemId,
                WarehouseId = receipt.WarehouseId,
                TransactionType = StockTransactionType.PurchaseReceiptReversal,
                SourceDocType = SourceDocumentType.DocumentReversal,
                SourceDocId = reversal.Id,
                SourceLineId = line.Id,
                QtyIn = 0m,
                QtyOut = line.ReceivedQty,
                UomId = line.UomId,
                BaseQty = stockEntry.BaseQty,
                RunningBalanceQty = runningBalance,
                TransactionDate = reversal.ReversalDate,
                CreatedBy = actor
            });
        }
    }

    private async Task CancelShortagesAsync(Guid receiptId, string actor, CancellationToken cancellationToken)
    {
        var shortages = await dbContext.ShortageLedgerEntries
            .Where(entity => entity.PurchaseReceiptId == receiptId && entity.Status != ShortageEntryStatus.Canceled)
            .ToListAsync(cancellationToken);

        foreach (var shortage in shortages)
        {
            shortage.ResolvedPhysicalQty = 0m;
            shortage.ResolvedFinancialQtyEquivalent = 0m;
            shortage.ResolvedAmount = 0m;
            shortage.OpenQty = 0m;
            shortage.OpenAmount = 0m;
            shortage.Status = ShortageEntryStatus.Canceled;
            shortage.UpdatedBy = actor;
        }
    }

    private static void EnsurePostedAndNotReversed(DocumentStatus status, Guid? reversalDocumentId, string label)
    {
        if (status != DocumentStatus.Posted)
        {
            throw new InvalidOperationException($"Only Posted {label}s can be reversed.");
        }

        if (reversalDocumentId.HasValue)
        {
            throw new InvalidOperationException($"This {label} has already been reversed.");
        }
    }
}
