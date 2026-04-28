using ERP.Application.Reversals;
using ERP.Application.Statements;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Domain.Shortages;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Reversals;

public sealed class ShortageResolutionReversalService(
    AppDbContext dbContext,
    ISupplierStatementPostingService statementPostingService) : IShortageResolutionReversalService
{
    public async Task<DocumentReversalDto?> ReverseAsync(Guid resolutionId, ReverseDocumentRequest request, string actor, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var resolution = await dbContext.ShortageResolutions
            .AsSplitQuery()
            .Include(entity => entity.Supplier)
            .Include(entity => entity.Allocations.OrderBy(allocation => allocation.SequenceNo))
                .ThenInclude(allocation => allocation.ShortageLedgerEntry!)
                    .ThenInclude(shortage => shortage.PurchaseReceipt)
            .Include(entity => entity.Allocations)
                .ThenInclude(allocation => allocation.ShortageLedgerEntry!)
                    .ThenInclude(shortage => shortage.ComponentItem)
            .SingleOrDefaultAsync(entity => entity.Id == resolutionId, cancellationToken);

        if (resolution is null)
        {
            return null;
        }

        EnsurePostedAndNotReversed(resolution.Status, resolution.ReversalDocumentId, "shortage resolution");
        await ValidateDependenciesAsync(resolution.Id, cancellationToken);

        var reversal = await DocumentReversalSupport.CreateAsync(
            dbContext,
            BusinessDocumentType.ShortageResolution,
            resolution.Id,
            request,
            actor,
            cancellationToken);

        await ReverseShortageStateAsync(resolution, reversal, actor, cancellationToken);
        await statementPostingService.CreateShortageResolutionReversalEntriesAsync(resolution, reversal, actor, cancellationToken);

        resolution.ReversalDocumentId = reversal.Id;
        resolution.ReversedAt = reversal.ReversalDate;
        resolution.UpdatedBy = actor;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return DocumentReversalSupport.ToDto(reversal);
    }

    private async Task ValidateDependenciesAsync(Guid resolutionId, CancellationToken cancellationToken)
    {
        var hasActivePayments = await dbContext.PaymentAllocations.AnyAsync(
            entity => entity.TargetDocType == Domain.Payments.PaymentTargetDocumentType.ShortageResolution &&
                      entity.TargetDocId == resolutionId &&
                      entity.Payment!.Status == DocumentStatus.Posted &&
                      entity.Payment.ReversalDocumentId == null,
            cancellationToken);

        if (hasActivePayments)
        {
            throw new InvalidOperationException("Shortage resolution reversal is blocked because active supplier payment allocations exist.");
        }
    }

    private async Task ReverseShortageStateAsync(
        ShortageResolution resolution,
        Domain.Reversals.DocumentReversal reversal,
        string actor,
        CancellationToken cancellationToken)
    {
        var runningBalances = new Dictionary<(Guid ItemId, Guid WarehouseId), decimal>();

        foreach (var allocation in resolution.Allocations.OrderBy(entity => entity.SequenceNo))
        {
            var shortage = allocation.ShortageLedgerEntry
                ?? throw new InvalidOperationException("Shortage resolution reversal requires shortage traceability.");

            if (allocation.AllocationType == ShortageAllocationType.Physical)
            {
                shortage.ResolvedPhysicalQty = Round(shortage.ResolvedPhysicalQty - (allocation.AllocatedQty ?? 0m));
            }
            else
            {
                shortage.ResolvedFinancialQtyEquivalent = Round(shortage.ResolvedFinancialQtyEquivalent - (allocation.FinancialQtyEquivalent ?? 0m));
                shortage.ResolvedAmount = Round(shortage.ResolvedAmount - (allocation.AllocatedAmount ?? 0m));
            }

            RecalculateShortageState(shortage, actor);

            if (allocation.AllocationType != ShortageAllocationType.Physical)
            {
                continue;
            }

            var receipt = shortage.PurchaseReceipt
                ?? throw new InvalidOperationException("Shortage resolution reversal requires receipt traceability.");
            var componentItem = shortage.ComponentItem
                ?? throw new InvalidOperationException("Shortage resolution reversal requires component item traceability.");

            var key = (shortage.ComponentItemId, receipt.WarehouseId);
            if (!runningBalances.TryGetValue(key, out var runningBalance))
            {
                runningBalance = await dbContext.StockLedgerEntries
                    .Where(entity => entity.ItemId == shortage.ComponentItemId && entity.WarehouseId == receipt.WarehouseId)
                    .OrderByDescending(entity => entity.TransactionDate)
                    .ThenByDescending(entity => entity.CreatedAt)
                    .ThenByDescending(entity => entity.Id)
                    .Select(entity => entity.RunningBalanceQty)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            var baseQty = Round(allocation.AllocatedQty ?? 0m);
            runningBalance -= baseQty;
            runningBalances[key] = runningBalance;

            dbContext.StockLedgerEntries.Add(new StockLedgerEntry
            {
                ItemId = shortage.ComponentItemId,
                WarehouseId = receipt.WarehouseId,
                TransactionType = StockTransactionType.ShortageResolutionReversal,
                SourceDocType = SourceDocumentType.DocumentReversal,
                SourceDocId = reversal.Id,
                SourceLineId = allocation.Id,
                QtyIn = 0m,
                QtyOut = baseQty,
                UomId = componentItem.BaseUomId,
                BaseQty = baseQty,
                RunningBalanceQty = runningBalance,
                TransactionDate = reversal.ReversalDate,
                UnitCost = allocation.ValuationRate,
                TotalCost = allocation.ValuationRate.HasValue ? Round(baseQty * allocation.ValuationRate.Value) : null,
                CreatedBy = actor
            });
        }
    }

    private static void RecalculateShortageState(ShortageLedgerEntry shortage, string actor)
    {
        if (shortage.ResolvedPhysicalQty < 0m)
        {
            shortage.ResolvedPhysicalQty = 0m;
        }

        if (shortage.ResolvedFinancialQtyEquivalent < 0m)
        {
            shortage.ResolvedFinancialQtyEquivalent = 0m;
        }

        if (shortage.ResolvedAmount < 0m)
        {
            shortage.ResolvedAmount = 0m;
        }

        var resolvedQtyEquivalent = Round(shortage.ResolvedPhysicalQty + shortage.ResolvedFinancialQtyEquivalent);
        shortage.OpenQty = Round(shortage.ShortageQty - resolvedQtyEquivalent);
        if (shortage.OpenQty < 0m)
        {
            shortage.OpenQty = 0m;
        }

        if (shortage.ShortageValue.HasValue && shortage.ShortageQty > 0m)
        {
            var rate = Round(shortage.ShortageValue.Value / shortage.ShortageQty);
            shortage.OpenAmount = Round(shortage.OpenQty * rate);
        }

        shortage.Status = shortage.OpenQty switch
        {
            0m when shortage.ShortageQty > 0m => ShortageEntryStatus.Resolved,
            _ when resolvedQtyEquivalent > 0m => ShortageEntryStatus.PartiallyResolved,
            _ => ShortageEntryStatus.Open
        };

        shortage.UpdatedBy = actor;
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
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
