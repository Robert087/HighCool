using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Application.Statements;
using ERP.Domain.Common;
using ERP.Domain.Purchasing;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Purchasing.PurchaseReceipts;

public sealed class PurchaseReceiptPostingService(
    AppDbContext dbContext,
    IPurchaseReceiptService purchaseReceiptService,
    IStockLedgerService stockLedgerService,
    IShortageDetectionService shortageDetectionService,
    IQuantityConversionService quantityConversionService,
    ISupplierStatementPostingService supplierStatementPostingService) : IPurchaseReceiptPostingService
{
    public async Task<PurchaseReceiptDto?> PostAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var receipt = await dbContext.PurchaseReceipts
            .AsSplitQuery()
            .Include(entity => entity.Supplier)
            .Include(entity => entity.Warehouse)
            .Include(entity => entity.PurchaseOrder)
            .Include(entity => entity.Lines.OrderBy(line => line.LineNo))
                .ThenInclude(entity => entity.PurchaseOrderLine)
            .Include(entity => entity.Lines)
                .ThenInclude(entity => entity.Item)
                    .ThenInclude(entity => entity!.Components)
                        .ThenInclude(entity => entity.ComponentItem)
            .Include(entity => entity.Lines)
                .ThenInclude(entity => entity.Components)
                    .ThenInclude(entity => entity.ComponentItem)
            .Include(entity => entity.Lines)
                .ThenInclude(entity => entity.Components)
                    .ThenInclude(entity => entity.ShortageReasonCode)
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (receipt is null)
        {
            return null;
        }

        if (receipt.Status == DocumentStatus.Posted)
        {
            await transaction.CommitAsync(cancellationToken);
            return await purchaseReceiptService.GetAsync(id, cancellationToken);
        }

        if (receipt.Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft purchase receipts can be posted.");
        }

        var existingPostingEffects = await dbContext.StockLedgerEntries
            .AnyAsync(entity => entity.SourceDocId == receipt.Id, cancellationToken);

        if (existingPostingEffects)
        {
            throw new InvalidOperationException("Posting effects already exist for this purchase receipt.");
        }

        var existingStatementEffects = await dbContext.SupplierStatementEntries
            .AnyAsync(
                entity => entity.SourceDocType == Domain.Statements.SupplierStatementSourceDocumentType.PurchaseReceipt &&
                          entity.SourceDocId == receipt.Id,
                cancellationToken);

        if (existingStatementEffects)
        {
            throw new InvalidOperationException("Supplier statement effects already exist for this purchase receipt.");
        }

        await ValidateReceiptAsync(receipt, cancellationToken);
        var stockEntries = await stockLedgerService.CreateEntriesAsync(receipt, actor, cancellationToken);
        await supplierStatementPostingService.CreatePurchaseReceiptEntriesAsync(receipt, stockEntries, actor, cancellationToken);
        await shortageDetectionService.CreateEntriesAsync(receipt, actor, cancellationToken);

        receipt.Status = DocumentStatus.Posted;
        receipt.UpdatedBy = actor;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await purchaseReceiptService.GetAsync(receipt.Id, cancellationToken);
    }

    private async Task ValidateReceiptAsync(PurchaseReceipt receipt, CancellationToken cancellationToken)
    {
        if (receipt.Supplier is null || !receipt.Supplier.IsActive)
        {
            throw new InvalidOperationException("Supplier was not found.");
        }

        if (receipt.Warehouse is null || !receipt.Warehouse.IsActive)
        {
            throw new InvalidOperationException("Warehouse was not found.");
        }

        if (receipt.Lines.Count == 0)
        {
            throw new InvalidOperationException("At least one purchase receipt line is required before posting.");
        }

        if (receipt.SupplierPayableAmount < 0m)
        {
            throw new InvalidOperationException("Supplier payable amount cannot be negative.");
        }

        Dictionary<Guid, decimal> postedReceiptTotals = [];
        Dictionary<Guid, PurchaseOrderLine> purchaseOrderLines = [];

        if (receipt.PurchaseOrderId.HasValue)
        {
            var purchaseOrder = receipt.PurchaseOrder;
            if (purchaseOrder is null || purchaseOrder.Status != DocumentStatus.Posted)
            {
                throw new InvalidOperationException("Only posted purchase orders can be used for purchase receipt posting.");
            }

            if (purchaseOrder.SupplierId != receipt.SupplierId)
            {
                throw new InvalidOperationException("Purchase receipt supplier must match the linked purchase order supplier.");
            }

            purchaseOrderLines = await dbContext.PurchaseOrderLines
                .AsNoTracking()
                .Where(line => line.PurchaseOrderId == receipt.PurchaseOrderId.Value)
                .ToDictionaryAsync(line => line.Id, cancellationToken);

            var postedReceiptLines = await dbContext.PurchaseReceiptLines
                .AsNoTracking()
                .Where(line =>
                    line.PurchaseOrderLineId.HasValue &&
                    line.PurchaseReceiptId != receipt.Id &&
                    line.PurchaseReceipt!.PurchaseOrderId == receipt.PurchaseOrderId &&
                    line.PurchaseReceipt.Status == DocumentStatus.Posted)
                .Select(line => new
                {
                    PurchaseOrderLineId = line.PurchaseOrderLineId!.Value,
                    line.ReceivedQty
                })
                .ToListAsync(cancellationToken);

            postedReceiptTotals = postedReceiptLines
                .GroupBy(line => line.PurchaseOrderLineId)
                .ToDictionary(group => group.Key, group => group.Sum(line => line.ReceivedQty));
        }

        foreach (var line in receipt.Lines)
        {
            if (line.ReceivedQty <= 0m)
            {
                throw new InvalidOperationException("Purchase receipt line quantities must be greater than zero before posting.");
            }

            var item = line.Item;
            if (item is null || !item.IsActive)
            {
                throw new InvalidOperationException("Purchase receipt lines must reference active items before posting.");
            }

            await ConvertReceiptLineToBaseAsync(line, item, cancellationToken);

            if (receipt.PurchaseOrderId.HasValue)
            {
                if (!line.PurchaseOrderLineId.HasValue || !line.OrderedQtySnapshot.HasValue)
                {
                    throw new InvalidOperationException("Purchase order linked receipt lines require purchase order traceability.");
                }

                if (!purchaseOrderLines.TryGetValue(line.PurchaseOrderLineId.Value, out var purchaseOrderLine))
                {
                    throw new InvalidOperationException("Purchase order line was not found.");
                }

                if (line.ItemId != purchaseOrderLine.ItemId)
                {
                    throw new InvalidOperationException("Purchase receipt line item must match the linked purchase order line item.");
                }

                if (line.UomId != purchaseOrderLine.UomId)
                {
                    throw new InvalidOperationException("Purchase receipt line UOM must match the linked purchase order line UOM.");
                }

                if (line.OrderedQtySnapshot.Value != purchaseOrderLine.OrderedQty)
                {
                    throw new InvalidOperationException("Ordered quantity snapshot must match the linked purchase order line quantity.");
                }

                var alreadyReceived = postedReceiptTotals.TryGetValue(purchaseOrderLine.Id, out var total) ? total : 0m;
                var remainingQty = purchaseOrderLine.OrderedQty - alreadyReceived;
                if (line.ReceivedQty > remainingQty)
                {
                    throw new InvalidOperationException("Received quantity cannot exceed the remaining purchase order quantity.");
                }
            }

            foreach (var component in item.Components)
            {
                var componentItem = component.ComponentItem;
                if (componentItem is null || !componentItem.IsActive)
                {
                    throw new InvalidOperationException("Purchase receipt BOM components must reference active items before posting.");
                }

                await ConvertBomComponentToBaseAsync(line.LineNo, componentItem, component.Quantity, component.UomId, cancellationToken);
            }

            foreach (var actualComponent in line.Components)
            {
                var componentItem = actualComponent.ComponentItem;
                if (componentItem is null || !componentItem.IsActive)
                {
                    throw new InvalidOperationException("Purchase receipt actual components must reference active items before posting.");
                }

                if (actualComponent.ActualReceivedQty < 0m)
                {
                    throw new InvalidOperationException("Actual received component quantities must be zero or greater before posting.");
                }

                await ConvertActualComponentToBaseAsync(line.LineNo, componentItem, actualComponent.ActualReceivedQty, actualComponent.UomId, cancellationToken);
            }

            var expectedComponents = item.Components.ToDictionary(component => component.ComponentItemId);
            var actualComponents = line.Components.ToDictionary(component => component.ComponentItemId);
            var receivedBaseQty = await ConvertReceiptLineToBaseAsync(line, item, cancellationToken);

            var extraComponentIds = actualComponents.Keys.Except(expectedComponents.Keys).ToArray();
            if (extraComponentIds.Length > 0)
            {
                throw new InvalidOperationException($"Purchase receipt line {line.LineNo} contains component rows that are not defined on the selected item.");
            }

            var missingComponentIds = expectedComponents.Keys.Except(actualComponents.Keys).ToArray();
            if (missingComponentIds.Length > 0)
            {
                throw new InvalidOperationException($"Purchase receipt line {line.LineNo} is missing one or more auto-filled component rows.");
            }

            foreach (var expectedComponent in expectedComponents.Values)
            {
                var actualComponent = actualComponents[expectedComponent.ComponentItemId];
                var componentItem = expectedComponent.ComponentItem
                    ?? throw new InvalidOperationException("Purchase receipt posting requires component item references.");

                if (actualComponent.UomId != expectedComponent.UomId)
                {
                    throw new InvalidOperationException($"Purchase receipt line {line.LineNo} component {componentItem.Name} must use the item component UOM.");
                }

                var expectedQty = Round(receivedBaseQty * expectedComponent.Quantity);
                if (actualComponent.ExpectedQty != expectedQty)
                {
                    throw new InvalidOperationException($"Expected quantity for component {componentItem.Name} on line {line.LineNo} is out of date. Refresh the receipt line and try again.");
                }

            }
        }
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private async Task<decimal> ConvertReceiptLineToBaseAsync(
        PurchaseReceiptLine line,
        Domain.MasterData.Item item,
        CancellationToken cancellationToken)
    {
        try
        {
            return await quantityConversionService.ConvertAsync(
                line.ReceivedQty,
                line.UomId,
                item.BaseUomId,
                cancellationToken);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("global UOM conversion", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Purchase receipt line {line.LineNo} for item {item.Code} - {item.Name} requires a global UOM conversion from the receipt UOM to the item base UOM before posting.");
        }
    }

    private async Task<decimal> ConvertBomComponentToBaseAsync(
        int lineNo,
        Domain.MasterData.Item componentItem,
        decimal quantity,
        Guid fromUomId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await quantityConversionService.ConvertAsync(
                quantity,
                fromUomId,
                componentItem.BaseUomId,
                cancellationToken);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("global UOM conversion", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Purchase receipt BOM component setup for line {lineNo} item {componentItem.Code} - {componentItem.Name} requires a global UOM conversion to the component base UOM.");
        }
    }

    private async Task<decimal> ConvertActualComponentToBaseAsync(
        int lineNo,
        Domain.MasterData.Item componentItem,
        decimal quantity,
        Guid fromUomId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await quantityConversionService.ConvertAsync(
                quantity,
                fromUomId,
                componentItem.BaseUomId,
                cancellationToken);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("global UOM conversion", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Purchase receipt actual component quantity on line {lineNo} for item {componentItem.Code} - {componentItem.Name} requires a global UOM conversion to the component base UOM before posting.");
        }
    }
}
