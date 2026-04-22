using ERP.Application.Shortages;
using ERP.Domain.Inventory;
using ERP.Domain.Shortages;
using ERP.Domain.Statements;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Shortages;

public sealed class ShortageResolutionAllocationService(AppDbContext dbContext) : IShortageResolutionAllocationService
{
    public async Task ApplyAsync(ShortageResolution resolution, string actor, CancellationToken cancellationToken)
    {
        var runningStockBalances = new Dictionary<(Guid ItemId, Guid WarehouseId), decimal>();
        var runningSupplierBalances = new Dictionary<Guid, decimal>();

        foreach (var allocation in resolution.Allocations.OrderBy(entity => entity.SequenceNo))
        {
            var shortage = allocation.ShortageLedgerEntry
                ?? throw new InvalidOperationException("Resolution allocation is missing its shortage row.");
            NormalizeLegacyShortageState(shortage);
            var componentItem = shortage.ComponentItem
                ?? throw new InvalidOperationException("Shortage resolution requires component item references.");
            var receipt = shortage.PurchaseReceipt
                ?? throw new InvalidOperationException("Shortage resolution requires receipt traceability.");

            if (allocation.AllocationType == ShortageAllocationType.Physical)
            {
                var allocatedQty = Round(allocation.AllocatedQty!.Value);
                var rate = EnsureValuationBasis(shortage, allocation.ValuationRate);

                allocation.FinancialQtyEquivalent = null;
                ApplyPhysicalResolvedState(shortage, allocatedQty, actor);

                var stockKey = (shortage.ComponentItemId, receipt.WarehouseId);
                if (!runningStockBalances.TryGetValue(stockKey, out var stockRunningBalance))
                {
                    stockRunningBalance = await dbContext.StockLedgerEntries
                        .Where(entity => entity.ItemId == shortage.ComponentItemId && entity.WarehouseId == receipt.WarehouseId)
                        .OrderByDescending(entity => entity.TransactionDate)
                        .ThenByDescending(entity => entity.CreatedAt)
                        .Select(entity => entity.RunningBalanceQty)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                stockRunningBalance += allocatedQty;
                runningStockBalances[stockKey] = stockRunningBalance;

                dbContext.StockLedgerEntries.Add(new StockLedgerEntry
                {
                    ItemId = shortage.ComponentItemId,
                    WarehouseId = receipt.WarehouseId,
                    TransactionType = StockTransactionType.ShortagePhysicalResolution,
                    SourceDocType = SourceDocumentType.ShortageResolution,
                    SourceDocId = resolution.Id,
                    SourceLineId = allocation.Id,
                    QtyIn = allocatedQty,
                    QtyOut = 0m,
                    UomId = componentItem.BaseUomId,
                    BaseQty = allocatedQty,
                    RunningBalanceQty = stockRunningBalance,
                    TransactionDate = resolution.ResolutionDate,
                    UnitCost = rate,
                    TotalCost = rate.HasValue ? Round(allocatedQty * rate.Value) : null,
                    CreatedBy = actor
                });

                continue;
            }

            var valuationRate = EnsureValuationBasis(shortage, allocation.ValuationRate)
                ?? throw new InvalidOperationException("Financial allocations require a valuation basis.");
            var quantityEquivalent = Round(allocation.AllocatedQty!.Value);
            var allocatedAmount = Round(quantityEquivalent * valuationRate);

            allocation.AllocatedAmount = allocatedAmount;
            allocation.FinancialQtyEquivalent = quantityEquivalent;
            allocation.ValuationRate = valuationRate;
            ApplyFinancialResolvedState(shortage, allocatedAmount, quantityEquivalent, actor);

            if (!runningSupplierBalances.TryGetValue(resolution.SupplierId, out var supplierRunningBalance))
            {
                supplierRunningBalance = await dbContext.SupplierStatementEntries
                    .Where(entity => entity.SupplierId == resolution.SupplierId)
                    .OrderByDescending(entity => entity.TransactionDate)
                    .ThenByDescending(entity => entity.CreatedAt)
                    .Select(entity => entity.RunningBalance)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            supplierRunningBalance -= allocatedAmount;
            runningSupplierBalances[resolution.SupplierId] = supplierRunningBalance;

            dbContext.SupplierStatementEntries.Add(new SupplierStatementEntry
            {
                SupplierId = resolution.SupplierId,
                EffectType = SupplierStatementEffectType.ShortageFinancialResolution,
                SourceDocType = SupplierStatementSourceDocumentType.ShortageResolution,
                SourceDocId = resolution.Id,
                SourceLineId = allocation.Id,
                AmountDelta = -allocatedAmount,
                RunningBalance = supplierRunningBalance,
                Currency = resolution.Currency,
                TransactionDate = resolution.ResolutionDate,
                Notes = $"Shortage resolution {resolution.ResolutionNo}",
                CreatedBy = actor
            });
        }
    }

    private static decimal? EnsureValuationBasis(ShortageLedgerEntry shortage, decimal? providedRate)
    {
        var existingRate = shortage.ShortageValue.HasValue && shortage.ShortageQty > 0m
            ? Round(shortage.ShortageValue.Value / shortage.ShortageQty)
            : (decimal?)null;

        if (existingRate.HasValue)
        {
            if (providedRate.HasValue && Round(providedRate.Value) != existingRate.Value)
            {
                throw new InvalidOperationException("Allocation valuation rate must match the shortage row valuation basis.");
            }

            return existingRate;
        }

        if (!providedRate.HasValue)
        {
            return null;
        }

        var rate = Round(providedRate.Value);
        shortage.ShortageValue = Round(shortage.ShortageQty * rate);
        return rate;
    }

    private static void ApplyPhysicalResolvedState(ShortageLedgerEntry shortage, decimal resolvedQty, string actor)
    {
        shortage.ResolvedPhysicalQty = Round(shortage.ResolvedPhysicalQty + resolvedQty);
        RecalculateState(shortage, actor);
    }

    private static void ApplyFinancialResolvedState(
        ShortageLedgerEntry shortage,
        decimal resolvedAmount,
        decimal resolvedQtyEquivalent,
        string actor)
    {
        shortage.ResolvedFinancialQtyEquivalent = Round(shortage.ResolvedFinancialQtyEquivalent + resolvedQtyEquivalent);
        shortage.ResolvedAmount = Round(shortage.ResolvedAmount + resolvedAmount);
        RecalculateState(shortage, actor);
    }

    private static void RecalculateState(ShortageLedgerEntry shortage, string actor)
    {
        var resolvedQtyEquivalent = GetResolvedQtyEquivalent(shortage);
        shortage.OpenQty = ClampToZero(Round(shortage.ShortageQty - resolvedQtyEquivalent));

        if (shortage.ShortageValue.HasValue && shortage.ShortageQty > 0m)
        {
            var valuationRate = Round(shortage.ShortageValue.Value / shortage.ShortageQty);
            shortage.OpenAmount = ClampNullableToZero(Round(shortage.OpenQty * valuationRate));
        }

        shortage.Status = shortage.OpenQty switch
        {
            0m when shortage.ShortageQty > 0m => ShortageEntryStatus.Resolved,
            _ when resolvedQtyEquivalent > 0m => ShortageEntryStatus.PartiallyResolved,
            _ => ShortageEntryStatus.Open
        };
        shortage.UpdatedBy = actor;
    }

    private static decimal GetResolvedQtyEquivalent(ShortageLedgerEntry shortage)
    {
        return Round(shortage.ResolvedPhysicalQty + shortage.ResolvedFinancialQtyEquivalent);
    }

    private static decimal ClampToZero(decimal value)
    {
        return Math.Abs(value) < 0.000001m ? 0m : value;
    }

    private static decimal? ClampNullableToZero(decimal value)
    {
        return Math.Abs(value) < 0.000001m ? 0m : value;
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private static void NormalizeLegacyShortageState(ShortageLedgerEntry shortage)
    {
        if (shortage.Status is ShortageEntryStatus.Resolved or ShortageEntryStatus.Canceled)
        {
            return;
        }

        if (shortage.ResolvedPhysicalQty < 0m)
        {
            shortage.ResolvedPhysicalQty = 0m;
        }

        if (shortage.ResolvedFinancialQtyEquivalent < 0m)
        {
            shortage.ResolvedFinancialQtyEquivalent = 0m;
        }

        var resolvedQtyEquivalent = GetResolvedQtyEquivalent(shortage);
        if (shortage.OpenQty <= 0m && shortage.ShortageQty > resolvedQtyEquivalent)
        {
            shortage.OpenQty = Round(shortage.ShortageQty - resolvedQtyEquivalent);
        }

        if (shortage.ShortageValue.HasValue && shortage.ShortageQty > 0m)
        {
            var valuationRate = Round(shortage.ShortageValue.Value / shortage.ShortageQty);
            shortage.OpenAmount = Round(shortage.OpenQty * valuationRate);
        }
    }
}
