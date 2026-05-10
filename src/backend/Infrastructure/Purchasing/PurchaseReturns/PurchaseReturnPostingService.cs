using ERP.Application.Purchasing.PurchaseReturns;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Application.Statements;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Purchasing.PurchaseReturns;

public sealed class PurchaseReturnPostingService(
    AppDbContext dbContext,
    IPurchaseReturnService queryService,
    ISupplierStatementPostingService supplierStatementPostingService,
    IQuantityConversionService quantityConversionService) : IPurchaseReturnPostingService
{
    public async Task<PurchaseReturnDto?> PostAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var entity = await dbContext.PurchaseReturns
            .AsSplitQuery()
            .Include(item => item.Supplier)
            .Include(item => item.ReferenceReceipt)
            .Include(item => item.Lines.OrderBy(line => line.LineNo))
                .ThenInclude(line => line.Item)
            .Include(item => item.Lines)
                .ThenInclude(line => line.Warehouse)
            .Include(item => item.Lines)
                .ThenInclude(line => line.Uom)
            .Include(item => item.Lines)
                .ThenInclude(line => line.ReferenceReceiptLine)
                    .ThenInclude(line => line!.PurchaseReceipt)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        if (entity.Status == DocumentStatus.Posted)
        {
            await transaction.CommitAsync(cancellationToken);
            return await queryService.GetAsync(id, cancellationToken);
        }

        if (entity.Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft purchase returns can be posted.");
        }

        await ValidateAsync(entity, cancellationToken);
        await CreateStockEntriesAsync(entity, actor, cancellationToken);
        var returnAmount = await CalculateReturnAmountAsync(entity, cancellationToken);
        await supplierStatementPostingService.CreatePurchaseReturnEntriesAsync(entity, returnAmount, actor, cancellationToken);

        entity.Status = DocumentStatus.Posted;
        entity.UpdatedBy = actor;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await queryService.GetAsync(entity.Id, cancellationToken);
    }

    private async Task ValidateAsync(Domain.Purchasing.PurchaseReturn entity, CancellationToken cancellationToken)
    {
        if (entity.Lines.Count == 0)
        {
            throw new InvalidOperationException("At least one purchase return line is required before posting.");
        }

        if (entity.Supplier is null || !entity.Supplier.IsActive)
        {
            throw new InvalidOperationException("Supplier was not found.");
        }

        var lineIds = entity.Lines
            .Where(line => line.ReferenceReceiptLineId.HasValue)
            .Select(line => line.ReferenceReceiptLineId!.Value)
            .ToArray();

        var postedTotals = lineIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await dbContext.PurchaseReturnLines
                .AsNoTracking()
                .Where(line =>
                    line.ReferenceReceiptLineId.HasValue &&
                    lineIds.Contains(line.ReferenceReceiptLineId.Value) &&
                    line.PurchaseReturn!.Status == DocumentStatus.Posted &&
                    line.PurchaseReturn.ReversalDocumentId == null &&
                    line.PurchaseReturnId != entity.Id)
                .GroupBy(line => line.ReferenceReceiptLineId!.Value)
                .ToDictionaryAsync(group => group.Key, group => decimal.Round(group.Sum(line => line.BaseQty), 6, MidpointRounding.AwayFromZero), cancellationToken);

        foreach (var line in entity.Lines)
        {
            if (line.ReturnQty <= 0m)
            {
                throw new InvalidOperationException("Purchase return quantities must be greater than zero before posting.");
            }

            if (line.ReferenceReceiptLine is null)
            {
                continue;
            }

            if (line.Item is null)
            {
                throw new InvalidOperationException("Purchase return line is missing item information required for quantity validation.");
            }

            var receivedBaseQty = await quantityConversionService.ConvertAsync(
                line.ReferenceReceiptLine.ReceivedQty,
                line.ReferenceReceiptLine.UomId,
                line.Item.BaseUomId,
                cancellationToken);

            var alreadyReturned = postedTotals.TryGetValue(line.ReferenceReceiptLineId!.Value, out var value) ? value : 0m;
            var receivableBaseQty = ClampToZero(decimal.Round(receivedBaseQty - alreadyReturned, 6, MidpointRounding.AwayFromZero));

            if (line.BaseQty > receivableBaseQty)
            {
                throw new InvalidOperationException($"Purchase return line {line.LineNo} exceeds the remaining returnable quantity.");
            }
        }
    }

    private async Task CreateStockEntriesAsync(Domain.Purchasing.PurchaseReturn entity, string actor, CancellationToken cancellationToken)
    {
        var runningBalances = new Dictionary<(Guid ItemId, Guid WarehouseId), decimal>();

        foreach (var line in entity.Lines.OrderBy(item => item.LineNo))
        {
            var key = (line.ItemId, line.WarehouseId);
            if (!runningBalances.TryGetValue(key, out var runningBalance))
            {
                runningBalance = await dbContext.StockLedgerEntries
                    .Where(item => item.ItemId == line.ItemId && item.WarehouseId == line.WarehouseId)
                    .OrderByDescending(item => item.TransactionDate)
                    .ThenByDescending(item => item.CreatedAt)
                    .ThenByDescending(item => item.Id)
                    .Select(item => item.RunningBalanceQty)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            runningBalance -= line.BaseQty;
            runningBalances[key] = runningBalance;

            dbContext.StockLedgerEntries.Add(new StockLedgerEntry
            {
                ItemId = line.ItemId,
                WarehouseId = line.WarehouseId,
                TransactionType = StockTransactionType.PurchaseReturn,
                SourceDocType = SourceDocumentType.PurchaseReturn,
                SourceDocId = entity.Id,
                SourceLineId = line.Id,
                QtyIn = 0m,
                QtyOut = line.ReturnQty,
                UomId = line.UomId,
                BaseQty = line.BaseQty,
                RunningBalanceQty = runningBalance,
                TransactionDate = entity.ReturnDate,
                CreatedBy = actor
            });
        }
    }

    private async Task<decimal> CalculateReturnAmountAsync(Domain.Purchasing.PurchaseReturn entity, CancellationToken cancellationToken)
    {
        if (!entity.ReferenceReceiptId.HasValue || entity.ReferenceReceipt is null)
        {
            return 0m;
        }

        var referenceLines = await dbContext.PurchaseReceiptLines
            .AsNoTracking()
            .Include(line => line.Item)
            .Where(line => line.PurchaseReceiptId == entity.ReferenceReceiptId.Value)
            .ToListAsync(cancellationToken);

        var totalBaseQty = 0m;
        foreach (var referenceLine in referenceLines)
        {
            var item = referenceLine.Item
                ?? throw new InvalidOperationException("Purchase return amount calculation requires item base UOM data on the reference receipt.");

            totalBaseQty = decimal.Round(
                totalBaseQty + await quantityConversionService.ConvertAsync(
                    referenceLine.ReceivedQty,
                    referenceLine.UomId,
                    item.BaseUomId,
                    cancellationToken),
                6,
                MidpointRounding.AwayFromZero);
        }

        if (totalBaseQty <= 0m || entity.ReferenceReceipt.SupplierPayableAmount <= 0m)
        {
            return 0m;
        }

        var returnedBaseQty = decimal.Round(entity.Lines.Where(line => line.ReferenceReceiptLineId.HasValue).Sum(line => line.BaseQty), 6, MidpointRounding.AwayFromZero);
        if (returnedBaseQty <= 0m)
        {
            return 0m;
        }

        var proportion = returnedBaseQty / totalBaseQty;
        return decimal.Round(entity.ReferenceReceipt.SupplierPayableAmount * proportion, 6, MidpointRounding.AwayFromZero);
    }

    private static decimal ClampToZero(decimal value)
    {
        return Math.Abs(value) < 0.000001m ? 0m : Math.Max(value, 0m);
    }
}
