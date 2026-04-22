using ERP.Application.Shortages;
using ERP.Domain.Shortages;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Shortages;

public sealed class ShortageResolutionValidationService(AppDbContext dbContext) : IShortageResolutionValidationService
{
    public async Task ValidateDraftAsync(ShortageResolution resolution, CancellationToken cancellationToken)
    {
        if (resolution.Status != Domain.Common.DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft shortage resolutions can be posted.");
        }

        var supplier = await dbContext.Suppliers
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == resolution.SupplierId, cancellationToken);

        if (supplier is null || !supplier.IsActive)
        {
            throw new InvalidOperationException("Supplier was not found.");
        }

        if (resolution.Allocations.Count == 0)
        {
            throw new InvalidOperationException("At least one allocation is required before posting.");
        }

        var duplicateShortageIds = resolution.Allocations
            .GroupBy(entity => entity.ShortageLedgerId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateShortageIds.Length > 0)
        {
            throw new InvalidOperationException("Duplicate shortage allocations are not allowed inside the same resolution.");
        }

        foreach (var allocation in resolution.Allocations.OrderBy(entity => entity.SequenceNo))
        {
            var shortage = allocation.ShortageLedgerEntry
                ?? throw new InvalidOperationException("Each allocation must reference a valid shortage row.");
            NormalizeLegacyShortageState(shortage);

            if (shortage.PurchaseReceipt?.SupplierId != resolution.SupplierId)
            {
                throw new InvalidOperationException("Every allocation must belong to the selected supplier.");
            }

            if (shortage.Status is ShortageEntryStatus.Canceled or ShortageEntryStatus.Resolved || shortage.OpenQty <= 0m)
            {
                throw new InvalidOperationException("Resolved or canceled shortage rows cannot be allocated again.");
            }

            if (resolution.ResolutionType == ShortageResolutionType.Physical)
            {
                ValidatePhysicalAllocation(allocation, shortage);
                continue;
            }

            ValidateFinancialAllocation(allocation, shortage);
        }
    }

    private static void ValidatePhysicalAllocation(ShortageResolutionAllocation allocation, ShortageLedgerEntry shortage)
    {
        if (!allocation.AllocatedQty.HasValue || allocation.AllocatedQty.Value <= 0m)
        {
            throw new InvalidOperationException("Physical resolutions require allocated quantities.");
        }

        if (allocation.AllocatedAmount.HasValue)
        {
            throw new InvalidOperationException("Physical resolutions cannot include allocated amounts.");
        }

        if (allocation.ValuationRate.HasValue)
        {
            throw new InvalidOperationException("Physical resolutions do not require a valuation rate.");
        }

        if (allocation.AllocationType != ShortageAllocationType.Physical)
        {
            throw new InvalidOperationException("Physical resolution allocations must be marked as Physical.");
        }

        if (allocation.AllocatedQty.Value > shortage.OpenQty)
        {
            throw new InvalidOperationException("Allocated quantity cannot exceed the open shortage quantity.");
        }

        var projectedResolvedQty = Round(
            shortage.ResolvedPhysicalQty +
            shortage.ResolvedFinancialQtyEquivalent +
            allocation.AllocatedQty.Value);

        if (projectedResolvedQty > shortage.ShortageQty)
        {
            throw new InvalidOperationException("Cumulative shortage settlement cannot exceed the original shortage quantity.");
        }
    }

    private static void ValidateFinancialAllocation(ShortageResolutionAllocation allocation, ShortageLedgerEntry shortage)
    {
        if (!allocation.AllocatedQty.HasValue || allocation.AllocatedQty.Value <= 0m)
        {
            throw new InvalidOperationException("Financial resolutions require resolved quantity.");
        }

        if (allocation.AllocationType != ShortageAllocationType.Financial)
        {
            throw new InvalidOperationException("Financial resolution allocations must be marked as Financial.");
        }

        var rate = ResolveRate(shortage, allocation.ValuationRate);
        if (!rate.HasValue || rate.Value <= 0m)
        {
            throw new InvalidOperationException("Financial allocations require a valuation rate.");
        }

        var quantityEquivalent = Round(allocation.AllocatedQty.Value);
        if (quantityEquivalent <= 0m)
        {
            throw new InvalidOperationException("Financial quantity-equivalent must be greater than zero.");
        }

        if (quantityEquivalent > shortage.OpenQty)
        {
            throw new InvalidOperationException("Resolved quantity cannot exceed the open shortage quantity.");
        }

        var allocatedAmount = Round(quantityEquivalent * rate.Value);
        var openAmount = Round(shortage.OpenQty * rate.Value);
        if (allocatedAmount > openAmount)
        {
            throw new InvalidOperationException("Calculated amount cannot exceed the open shortage amount.");
        }

        var projectedResolvedQty = Round(
            shortage.ResolvedPhysicalQty +
            shortage.ResolvedFinancialQtyEquivalent +
            quantityEquivalent);

        if (projectedResolvedQty > shortage.ShortageQty)
        {
            throw new InvalidOperationException("Cumulative shortage settlement cannot exceed the original shortage quantity.");
        }
    }

    private static decimal? ResolveRate(ShortageLedgerEntry shortage, decimal? valuationRate)
    {
        if (shortage.ShortageValue.HasValue && shortage.ShortageQty > 0m)
        {
            var existingRate = Round(shortage.ShortageValue.Value / shortage.ShortageQty);

            if (valuationRate.HasValue && Round(valuationRate.Value) != existingRate)
            {
                throw new InvalidOperationException("Allocation valuation rate must match the shortage row valuation basis.");
            }

            return existingRate;
        }

        return valuationRate.HasValue ? Round(valuationRate.Value) : null;
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
            var rate = Round(shortage.ShortageValue.Value / shortage.ShortageQty);
            shortage.OpenAmount = Round(shortage.OpenQty * rate);
        }

        shortage.Status = shortage.OpenQty switch
        {
            0m when shortage.ShortageQty > 0m => ShortageEntryStatus.Resolved,
            _ when resolvedQtyEquivalent > 0m => ShortageEntryStatus.PartiallyResolved,
            _ => ShortageEntryStatus.Open
        };
    }

    private static decimal GetResolvedQtyEquivalent(ShortageLedgerEntry shortage)
    {
        return Round(shortage.ResolvedPhysicalQty + shortage.ResolvedFinancialQtyEquivalent);
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }
}
