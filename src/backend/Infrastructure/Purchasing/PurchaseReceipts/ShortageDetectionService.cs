using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Domain.Purchasing;
using ERP.Domain.Shortages;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Purchasing.PurchaseReceipts;

public sealed class ShortageDetectionService(
    AppDbContext dbContext,
    IQuantityConversionService quantityConversionService) : IShortageDetectionService
{
    private const int Scale = 6;

    public async Task<IReadOnlyList<ShortageLedgerEntry>> CreateEntriesAsync(
        PurchaseReceipt receipt,
        string actor,
        CancellationToken cancellationToken)
    {
        var entries = new List<ShortageLedgerEntry>();
        var reasonIds = receipt.Lines
            .SelectMany(line => line.Components)
            .Where(component => component.ShortageReasonCodeId.HasValue)
            .Select(component => component.ShortageReasonCodeId!.Value)
            .Distinct()
            .ToArray();

        var reasons = await dbContext.ShortageReasonCodes
            .AsNoTracking()
            .Where(entity => reasonIds.Contains(entity.Id) && entity.IsActive)
            .ToDictionaryAsync(entity => entity.Id, cancellationToken);

        foreach (var line in receipt.Lines.OrderBy(entity => entity.LineNo))
        {
            var item = line.Item ?? throw new InvalidOperationException("Purchase receipt posting requires item references.");
            var expectedComponents = item.Components.ToDictionary(entity => entity.ComponentItemId);
            var actualComponents = line.Components.ToDictionary(entity => entity.ComponentItemId);

            var extraComponentIds = actualComponents.Keys.Except(expectedComponents.Keys).ToArray();
            if (extraComponentIds.Length > 0)
            {
                throw new InvalidOperationException("Purchase receipt line components must match the item's embedded BOM components.");
            }

            var missingComponentIds = expectedComponents.Keys.Except(actualComponents.Keys).ToArray();
            if (missingComponentIds.Length > 0)
            {
                throw new InvalidOperationException("Actual component rows are required for every BOM component before posting.");
            }

            if (expectedComponents.Count == 0)
            {
                if (actualComponents.Count > 0)
                {
                    throw new InvalidOperationException("Items without BOM components cannot capture actual component rows.");
                }

                continue;
            }

            foreach (var expectedComponent in expectedComponents.Values.OrderBy(entity => entity.ComponentItem!.Code))
            {
                var componentItem = expectedComponent.ComponentItem
                    ?? throw new InvalidOperationException("Purchase receipt posting requires component item references.");
                var actualComponent = actualComponents[expectedComponent.ComponentItemId];

                var expectedQtyInRowUom = actualComponent.ExpectedQty;
                decimal expectedQty;
                decimal actualQty;

                try
                {
                    expectedQty = await quantityConversionService.ConvertAsync(
                        expectedQtyInRowUom,
                        actualComponent.UomId,
                        componentItem.BaseUomId,
                        cancellationToken);
                    actualQty = await quantityConversionService.ConvertAsync(
                        actualComponent.ActualReceivedQty,
                        actualComponent.UomId,
                        componentItem.BaseUomId,
                        cancellationToken);
                }
                catch (InvalidOperationException exception) when (exception.Message.Contains("global UOM conversion", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Purchase receipt component shortage calculation on line {line.LineNo} for item {componentItem.Code} - {componentItem.Name} requires a global UOM conversion to the component base UOM.");
                }
                var shortageQty = Round(expectedQty - actualQty);

                if (shortageQty <= 0m)
                {
                    continue;
                }

                ShortageReasonCode? reason = null;
                if (actualComponent.ShortageReasonCodeId.HasValue &&
                    !reasons.TryGetValue(actualComponent.ShortageReasonCodeId.Value, out reason))
                {
                    throw new InvalidOperationException("The shortage reason was not found or is inactive.");
                }

                entries.Add(new ShortageLedgerEntry
                {
                    PurchaseReceiptId = receipt.Id,
                    PurchaseReceiptLineId = line.Id,
                    PurchaseOrderId = receipt.PurchaseOrderId,
                    PurchaseOrderLineId = line.PurchaseOrderLineId,
                    ItemId = line.ItemId,
                    ComponentItemId = expectedComponent.ComponentItemId,
                    ExpectedQty = expectedQty,
                    ActualQty = actualQty,
                    ShortageQty = shortageQty,
                    ResolvedPhysicalQty = 0m,
                    ResolvedFinancialQtyEquivalent = 0m,
                    OpenQty = shortageQty,
                    ShortageValue = null,
                    ResolvedAmount = 0m,
                    OpenAmount = null,
                    ShortageReasonCodeId = reason?.Id,
                    AffectsSupplierBalance = reason?.AffectsSupplierBalance ?? false,
                    ApprovalStatus = reason?.RequiresApproval == true ? "PendingApproval" : "NotRequired",
                    Status = ShortageEntryStatus.Open,
                    CreatedBy = actor
                });
            }
        }

        dbContext.ShortageLedgerEntries.AddRange(entries);
        return entries;
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, Scale, MidpointRounding.AwayFromZero);
    }
}
