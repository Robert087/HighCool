using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Domain.Common;
using ERP.Domain.MasterData;
using ERP.Domain.Purchasing;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Purchasing.PurchaseReceipts;

public sealed class PurchaseReceiptService(
    AppDbContext dbContext,
    IQuantityConversionService quantityConversionService) : IPurchaseReceiptService
{
    private const int QuantityScale = 6;

    public async Task<PagedResult<PurchaseReceiptListItemDto>> ListAsync(PurchaseReceiptListQuery query, CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);

        var receipts = dbContext.PurchaseReceipts
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            receipts = receipts.Where(entity =>
                entity.ReceiptNo.Contains(search) ||
                entity.Supplier!.Code.Contains(search) ||
                entity.Supplier.Name.Contains(search) ||
                entity.Warehouse!.Code.Contains(search) ||
                entity.Warehouse.Name.Contains(search) ||
                (entity.PurchaseOrder != null && entity.PurchaseOrder.PoNo.Contains(search)) ||
                (entity.Notes != null && entity.Notes.Contains(search)));
        }

        if (query.Status.HasValue)
        {
            receipts = receipts.Where(entity => entity.Status == query.Status.Value);
        }

        if (query.LinkedToPurchaseOrder.HasValue)
        {
            receipts = query.LinkedToPurchaseOrder.Value
                ? receipts.Where(entity => entity.PurchaseOrderId != null)
                : receipts.Where(entity => entity.PurchaseOrderId == null);
        }

        if (query.FromDate.HasValue)
        {
            receipts = receipts.Where(entity => entity.ReceiptDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            receipts = receipts.Where(entity => entity.ReceiptDate <= query.ToDate.Value);
        }

        receipts = ApplySorting(receipts, query);

        var totalCount = await receipts.CountAsync(cancellationToken);
        var items = await receipts
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(entity => new PurchaseReceiptListItemDto(
                entity.Id,
                entity.ReceiptNo,
                entity.SupplierId,
                entity.Supplier!.Code,
                entity.Supplier.Name,
                entity.WarehouseId,
                entity.Warehouse!.Code,
                entity.Warehouse.Name,
                entity.PurchaseOrderId,
                entity.PurchaseOrder != null ? entity.PurchaseOrder.PoNo : null,
                entity.ReceiptDate,
                entity.Status,
                entity.Lines.Count,
                entity.ReversalDocumentId,
                entity.ReversedAt,
                entity.CreatedAt,
                entity.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<PurchaseReceiptListItemDto>(
            items,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            CalculateTotalPages(totalCount, pagination.NormalizedPageSize),
            new
            {
                query.Search,
                query.Status,
                query.LinkedToPurchaseOrder,
                query.FromDate,
                query.ToDate
            },
            new PagedSort(ResolveSortBy(query.SortBy), query.SortDirection));
    }

    public async Task<PurchaseReceiptDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await IncludeReceiptDetails()
            .SingleOrDefaultAsync(receipt => receipt.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var returnStateByLineId = entity.Status == DocumentStatus.Posted && entity.ReversalDocumentId is null
            ? await BuildReturnStateByReceiptLineIdAsync(entity.Lines.ToArray(), cancellationToken)
            : entity.Lines.ToDictionary(line => line.Id, _ => new ReceiptLineReturnState(0m, 0m));

        return ToDto(entity, returnStateByLineId);
    }

    public async Task<PurchaseReceiptDto> CreateDraftAsync(UpsertPurchaseReceiptDraftRequest request, string actor, CancellationToken cancellationToken)
    {
        var receiptNo = await ResolveReceiptNoAsync(request.ReceiptNo, null, cancellationToken);
        var itemDefinitions = await ValidateDraftRequestAsync(request, cancellationToken);

        var receipt = new PurchaseReceipt
        {
            ReceiptNo = receiptNo,
            SupplierId = request.SupplierId,
            WarehouseId = request.WarehouseId,
            PurchaseOrderId = request.PurchaseOrderId,
            ReceiptDate = request.ReceiptDate!.Value,
            SupplierPayableAmount = Round(request.SupplierPayableAmount),
            Notes = NormalizeOptionalText(request.Notes),
            Status = DocumentStatus.Draft,
            CreatedBy = actor
        };

        dbContext.PurchaseReceipts.Add(receipt);
        await AddLinesAsync(receipt, request.Lines, itemDefinitions, actor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(receipt.Id, cancellationToken);
    }

    public async Task<PurchaseReceiptDto?> UpdateDraftAsync(Guid id, UpsertPurchaseReceiptDraftRequest request, string actor, CancellationToken cancellationToken)
    {
        var receipt = await dbContext.PurchaseReceipts
            .Include(entity => entity.Lines)
                .ThenInclude(entity => entity.Components)
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (receipt is null)
        {
            return null;
        }

        EnsureDraftIsEditable(receipt);

        receipt.ReceiptNo = await ResolveReceiptNoAsync(request.ReceiptNo, id, cancellationToken);
        var itemDefinitions = await ValidateDraftRequestAsync(request, cancellationToken);

        receipt.SupplierId = request.SupplierId;
        receipt.WarehouseId = request.WarehouseId;
        receipt.PurchaseOrderId = request.PurchaseOrderId;
        receipt.ReceiptDate = request.ReceiptDate!.Value;
        receipt.SupplierPayableAmount = Round(request.SupplierPayableAmount);
        receipt.Notes = NormalizeOptionalText(request.Notes);
        receipt.UpdatedBy = actor;

        dbContext.PurchaseReceiptLineComponents.RemoveRange(receipt.Lines.SelectMany(line => line.Components));
        dbContext.PurchaseReceiptLines.RemoveRange(receipt.Lines);
        await AddLinesAsync(receipt, request.Lines, itemDefinitions, actor, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredAsync(receipt.Id, cancellationToken);
    }

    private IQueryable<PurchaseReceipt> IncludeReceiptDetails()
    {
        return dbContext.PurchaseReceipts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(entity => entity.Supplier)
            .Include(entity => entity.Warehouse)
            .Include(entity => entity.PurchaseOrder)
            .Include(entity => entity.Lines.OrderBy(line => line.LineNo))
                .ThenInclude(entity => entity.PurchaseOrderLine)
            .Include(entity => entity.Lines)
                .ThenInclude(entity => entity.Item)
            .Include(entity => entity.Lines)
                .ThenInclude(entity => entity.Uom)
            .Include(entity => entity.Lines)
                .ThenInclude(entity => entity.Components)
                    .ThenInclude(entity => entity.ComponentItem)
            .Include(entity => entity.Lines)
                .ThenInclude(entity => entity.Components)
                    .ThenInclude(entity => entity.Uom)
            .Include(entity => entity.Lines)
                .ThenInclude(entity => entity.Components)
                    .ThenInclude(entity => entity.ShortageReasonCode);
    }

    private async Task<Dictionary<Guid, Item>> ValidateDraftRequestAsync(
        UpsertPurchaseReceiptDraftRequest request,
        CancellationToken cancellationToken)
    {
        var supplierExists = await dbContext.Suppliers.AnyAsync(entity => entity.Id == request.SupplierId && entity.IsActive, cancellationToken);
        if (!supplierExists)
        {
            throw new InvalidOperationException("Supplier was not found.");
        }

        var warehouseExists = await dbContext.Warehouses.AnyAsync(entity => entity.Id == request.WarehouseId && entity.IsActive, cancellationToken);
        if (!warehouseExists)
        {
            throw new InvalidOperationException("Warehouse was not found.");
        }

        var purchaseOrder = request.PurchaseOrderId.HasValue
            ? await dbContext.PurchaseOrders
                .AsNoTracking()
                .Include(entity => entity.Lines)
                .SingleOrDefaultAsync(entity => entity.Id == request.PurchaseOrderId.Value, cancellationToken)
            : null;

        if (request.PurchaseOrderId.HasValue)
        {
            if (purchaseOrder is null)
            {
                throw new InvalidOperationException("Purchase order was not found.");
            }

            if (purchaseOrder.Status != DocumentStatus.Posted)
            {
                throw new InvalidOperationException("Only posted purchase orders can be used for purchase receipts.");
            }

            if (purchaseOrder.SupplierId != request.SupplierId)
            {
                throw new InvalidOperationException("Purchase receipt supplier must match the linked purchase order supplier.");
            }

            var duplicatePurchaseOrderLines = request.Lines
                .Where(line => line.PurchaseOrderLineId.HasValue)
                .GroupBy(line => line.PurchaseOrderLineId!.Value)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            if (duplicatePurchaseOrderLines.Length > 0)
            {
                throw new DuplicateEntityException("Each purchase order line can only appear once inside a purchase receipt.");
            }
        }

        var itemIds = request.Lines.Select(line => line.ItemId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        var itemDefinitions = itemIds.Length == 0
            ? []
            : await dbContext.Items
                .AsNoTracking()
                .Include(entity => entity.BaseUom)
                .Include(entity => entity.Components)
                    .ThenInclude(component => component.ComponentItem)
                        .ThenInclude(componentItem => componentItem!.BaseUom)
                .Include(entity => entity.Components)
                    .ThenInclude(component => component.Uom)
                .Where(entity => itemIds.Contains(entity.Id) && entity.IsActive)
                .ToDictionaryAsync(entity => entity.Id, cancellationToken);

        if (itemDefinitions.Count != itemIds.Length)
        {
            throw new InvalidOperationException("One or more item references were not found.");
        }

        var lineUomIds = request.Lines
            .Select(line => line.UomId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (lineUomIds.Length > 0)
        {
            var existingUomIds = await dbContext.Uoms
                .Where(entity => lineUomIds.Contains(entity.Id) && entity.IsActive)
                .Select(entity => entity.Id)
                .ToListAsync(cancellationToken);

            if (existingUomIds.Count != lineUomIds.Length)
            {
                throw new InvalidOperationException("One or more UOM references were not found.");
            }
        }

        var shortageReasonIds = request.Lines
            .SelectMany(line => line.Components)
            .Where(component => component.ShortageReasonCodeId.HasValue)
            .Select(component => component.ShortageReasonCodeId!.Value)
            .Distinct()
            .ToArray();

        if (shortageReasonIds.Length > 0)
        {
            var existingShortageReasonIds = await dbContext.ShortageReasonCodes
                .Where(entity => shortageReasonIds.Contains(entity.Id) && entity.IsActive)
                .Select(entity => entity.Id)
                .ToListAsync(cancellationToken);

            if (existingShortageReasonIds.Count != shortageReasonIds.Length)
            {
                throw new InvalidOperationException("One or more shortage reason references were not found.");
            }
        }

        var duplicateLineNumbers = request.Lines
            .GroupBy(line => line.LineNo)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateLineNumbers.Length > 0)
        {
            throw new DuplicateEntityException("Line numbers must be unique inside the purchase receipt draft.");
        }

        foreach (var line in request.Lines)
        {
            var duplicateComponentRows = line.Components
                .GroupBy(component => component.ComponentItemId)
                .Where(group => group.Key != Guid.Empty && group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            if (duplicateComponentRows.Length > 0)
            {
                throw new DuplicateEntityException("No duplicate component item rows are allowed inside the same purchase receipt line.");
            }

            var item = itemDefinitions[line.ItemId];
            var expectedComponentIds = item.Components.Select(component => component.ComponentItemId).ToHashSet();
            var submittedComponentIds = line.Components.Select(component => component.ComponentItemId).Where(id => id != Guid.Empty).ToArray();

            var extraComponentIds = submittedComponentIds.Where(id => !expectedComponentIds.Contains(id)).ToArray();
            if (extraComponentIds.Length > 0)
            {
                throw new InvalidOperationException($"Purchase receipt line {line.LineNo} contains component rows that are not defined on the selected item.");
            }

            if (!item.HasComponents && submittedComponentIds.Length > 0)
            {
                throw new InvalidOperationException($"Purchase receipt line {line.LineNo} cannot contain component rows because the selected item has no components.");
            }

            var expectedComponents = item.Components.ToDictionary(component => component.ComponentItemId);
            var receivedBaseQty = await ConvertLineQtyToBaseAsync(line, item, cancellationToken);

            foreach (var submittedComponent in line.Components)
            {
                var componentDefinition = expectedComponents[submittedComponent.ComponentItemId];
                var componentName = componentDefinition.ComponentItem?.Name ?? submittedComponent.ComponentItemId.ToString();

                if (submittedComponent.UomId != componentDefinition.UomId)
                {
                    throw new InvalidOperationException(
                        $"Purchase receipt line {line.LineNo} component {componentName} must use the item component UOM.");
                }

                var expectedQty = Round(receivedBaseQty * componentDefinition.Quantity);
            }
        }

        if (purchaseOrder is null)
        {
            if (request.Lines.Any(line => line.PurchaseOrderLineId.HasValue))
            {
                throw new InvalidOperationException("Purchase order line links require a purchase order header link.");
            }

            return itemDefinitions;
        }

        var postedReceiptLines = await dbContext.PurchaseReceiptLines
            .AsNoTracking()
            .Where(line =>
                line.PurchaseOrderLineId.HasValue &&
                line.PurchaseReceipt!.PurchaseOrderId == purchaseOrder.Id &&
                line.PurchaseReceipt.Status == DocumentStatus.Posted &&
                line.PurchaseReceipt.ReversalDocumentId == null)
            .Select(line => new
            {
                PurchaseOrderLineId = line.PurchaseOrderLineId!.Value,
                line.ReceivedQty
            })
            .ToListAsync(cancellationToken);

        var postedReceiptTotals = postedReceiptLines
            .GroupBy(line => line.PurchaseOrderLineId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.ReceivedQty));

        var purchaseOrderLines = purchaseOrder.Lines.ToDictionary(line => line.Id);

        foreach (var line in request.Lines)
        {
            if (!line.PurchaseOrderLineId.HasValue)
            {
                throw new InvalidOperationException("Purchase order linked receipts require each line to reference a purchase order line.");
            }

            if (!line.OrderedQtySnapshot.HasValue)
            {
                throw new InvalidOperationException("Purchase order linked receipts require ordered quantity snapshots.");
            }

            if (!purchaseOrderLines.TryGetValue(line.PurchaseOrderLineId.Value, out var purchaseOrderLine))
            {
                throw new InvalidOperationException("One or more purchase order lines were not found.");
            }

            if (line.ItemId != purchaseOrderLine.ItemId)
            {
                throw new InvalidOperationException("Purchase receipt item must match the linked purchase order line item.");
            }

            if (line.UomId != purchaseOrderLine.UomId)
            {
                throw new InvalidOperationException("Purchase receipt UOM must match the linked purchase order line UOM.");
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

        return itemDefinitions;
    }

    private async Task AddLinesAsync(
        PurchaseReceipt receipt,
        IReadOnlyList<UpsertPurchaseReceiptLineRequest> lines,
        IReadOnlyDictionary<Guid, Item> itemDefinitions,
        string actor,
        CancellationToken cancellationToken)
    {
        foreach (var line in lines.OrderBy(entry => entry.LineNo))
        {
            var lineEntity = new PurchaseReceiptLine
            {
                PurchaseReceipt = receipt,
                LineNo = line.LineNo,
                PurchaseOrderLineId = line.PurchaseOrderLineId,
                ItemId = line.ItemId,
                OrderedQtySnapshot = line.OrderedQtySnapshot,
                ReceivedQty = line.ReceivedQty,
                UomId = line.UomId,
                Notes = NormalizeOptionalText(line.Notes),
                CreatedBy = actor
            };

            var itemDefinition = itemDefinitions[line.ItemId];
            var receivedBaseQty = await ConvertLineQtyToBaseAsync(line, itemDefinition, cancellationToken);

            var submittedComponentMap = line.Components.ToDictionary(component => component.ComponentItemId);
            foreach (var componentDefinition in itemDefinition.Components.OrderBy(component => component.ComponentItem?.Code))
            {
                var expectedQty = Round(receivedBaseQty * componentDefinition.Quantity);
                submittedComponentMap.TryGetValue(componentDefinition.ComponentItemId, out var submittedComponent);

                if (submittedComponent is not null &&
                    submittedComponent.UomId != Guid.Empty &&
                    submittedComponent.UomId != componentDefinition.UomId)
                {
                    throw new InvalidOperationException(
                        $"Purchase receipt line {line.LineNo} component {componentDefinition.ComponentItem?.Name ?? componentDefinition.ComponentItemId.ToString()} must use the item component UOM.");
                }

                lineEntity.Components.Add(new PurchaseReceiptLineComponent
                {
                    ComponentItemId = componentDefinition.ComponentItemId,
                    ExpectedQty = expectedQty,
                    ActualReceivedQty = submittedComponent?.ActualReceivedQty ?? expectedQty,
                    UomId = componentDefinition.UomId,
                    ShortageReasonCodeId = submittedComponent?.ShortageReasonCodeId,
                    Notes = NormalizeOptionalText(submittedComponent?.Notes),
                    CreatedBy = actor
                });
            }

            receipt.Lines.Add(lineEntity);
        }
    }

    private async Task<string> ResolveReceiptNoAsync(string? receiptNo, Guid? currentId, CancellationToken cancellationToken)
    {
        var value = string.IsNullOrWhiteSpace(receiptNo)
            ? $"PR-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
            : receiptNo.Trim();

        var exists = await dbContext.PurchaseReceipts.AnyAsync(entity => entity.ReceiptNo == value && entity.Id != currentId, cancellationToken);
        if (exists)
        {
            throw new DuplicateEntityException($"Purchase receipt number '{value}' already exists.");
        }

        return value;
    }

    private async Task<PurchaseReceiptDto> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        var dto = await GetAsync(id, cancellationToken);
        return dto ?? throw new InvalidOperationException("Purchase receipt was not found after save.");
    }

    private static void EnsureDraftIsEditable(PurchaseReceipt receipt)
    {
        if (receipt.Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft purchase receipts can be edited.");
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<decimal> ConvertLineQtyToBaseAsync(
        UpsertPurchaseReceiptLineRequest line,
        Item item,
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
            var baseUomLabel = item.BaseUom?.Code ?? item.BaseUomId.ToString();
            throw new InvalidOperationException(
                $"Purchase receipt line {line.LineNo} for item {item.Code} - {item.Name} requires a global UOM conversion from the selected receipt UOM to base UOM {baseUomLabel}.");
        }
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, QuantityScale, MidpointRounding.AwayFromZero);
    }

    private static IQueryable<PurchaseReceipt> ApplySorting(IQueryable<PurchaseReceipt> query, PurchaseReceiptListQuery request)
    {
        var sortBy = ResolveSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("receiptNo", true) => query.OrderBy(entity => entity.ReceiptNo).ThenBy(entity => entity.Id),
            ("receiptNo", false) => query.OrderByDescending(entity => entity.ReceiptNo).ThenByDescending(entity => entity.Id),
            ("supplierName", true) => query.OrderBy(entity => entity.Supplier!.Name).ThenBy(entity => entity.ReceiptDate),
            ("supplierName", false) => query.OrderByDescending(entity => entity.Supplier!.Name).ThenByDescending(entity => entity.ReceiptDate),
            ("warehouseName", true) => query.OrderBy(entity => entity.Warehouse!.Name).ThenBy(entity => entity.ReceiptDate),
            ("warehouseName", false) => query.OrderByDescending(entity => entity.Warehouse!.Name).ThenByDescending(entity => entity.ReceiptDate),
            ("status", true) => query.OrderBy(entity => entity.Status).ThenBy(entity => entity.ReceiptDate),
            ("status", false) => query.OrderByDescending(entity => entity.Status).ThenByDescending(entity => entity.ReceiptDate),
            _ when ascending => query.OrderBy(entity => entity.ReceiptDate).ThenBy(entity => entity.CreatedAt),
            _ => query.OrderByDescending(entity => entity.ReceiptDate).ThenByDescending(entity => entity.CreatedAt)
        };
    }

    private static string ResolveSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? "receiptDate" : sortBy.Trim();
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private async Task<IReadOnlyDictionary<Guid, ReceiptLineReturnState>> BuildReturnStateByReceiptLineIdAsync(
        IReadOnlyCollection<PurchaseReceiptLine> receiptLines,
        CancellationToken cancellationToken)
    {
        if (receiptLines.Count == 0)
        {
            return new Dictionary<Guid, ReceiptLineReturnState>();
        }

        var receiptLineIds = receiptLines.Select(line => line.Id).ToArray();

        var returnedRows = await dbContext.PurchaseReturnLines
            .AsNoTracking()
            .Where(line =>
                line.ReferenceReceiptLineId.HasValue &&
                receiptLineIds.Contains(line.ReferenceReceiptLineId.Value) &&
                line.PurchaseReturn!.Status == DocumentStatus.Posted &&
                line.PurchaseReturn.ReversalDocumentId == null)
            .Select(line => new
            {
                ReferenceReceiptLineId = line.ReferenceReceiptLineId!.Value,
                line.BaseQty
            })
            .ToListAsync(cancellationToken);

        var returnedByLineId = returnedRows
            .GroupBy(line => line.ReferenceReceiptLineId)
            .ToDictionary(group => group.Key, group => Round(group.Sum(line => line.BaseQty)));

        var stateByLineId = new Dictionary<Guid, ReceiptLineReturnState>();

        foreach (var line in receiptLines)
        {
            if (line.Item is null)
            {
                throw new InvalidOperationException("Purchase receipt line is missing item information required for returnable quantity conversion.");
            }

            var receivedBaseQty = await quantityConversionService.ConvertAsync(
                line.ReceivedQty,
                line.UomId,
                line.Item.BaseUomId,
                cancellationToken);

            var returnedBaseQty = returnedByLineId.TryGetValue(line.Id, out var returnedQty) ? returnedQty : 0m;
            var remainingBaseQty = ClampToZero(Round(receivedBaseQty - returnedBaseQty));

            var returnedDocumentQty = await ConvertBaseQtyToDocumentQtyAsync(returnedBaseQty, line, cancellationToken);
            var remainingDocumentQty = await ConvertBaseQtyToDocumentQtyAsync(remainingBaseQty, line, cancellationToken);

            stateByLineId[line.Id] = new ReceiptLineReturnState(returnedDocumentQty, remainingDocumentQty);
        }

        return stateByLineId;
    }

    private async Task<decimal> ConvertBaseQtyToDocumentQtyAsync(
        decimal baseQty,
        PurchaseReceiptLine line,
        CancellationToken cancellationToken)
    {
        if (baseQty == 0m)
        {
            return 0m;
        }

        if (line.Item is null)
        {
            throw new InvalidOperationException("Purchase receipt line is missing item information required for returnable quantity conversion.");
        }

        if (line.UomId == line.Item.BaseUomId)
        {
            return Round(baseQty);
        }

        var forwardConversion = await dbContext.UomConversions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entity => entity.FromUomId == line.UomId &&
                          entity.ToUomId == line.Item.BaseUomId &&
                          entity.IsActive,
                cancellationToken);

        if (forwardConversion is null || forwardConversion.Factor == 0m)
        {
            throw new InvalidOperationException("A required global UOM conversion could not be resolved.");
        }

        return Round(baseQty / forwardConversion.Factor);
    }

    private static PurchaseReceiptDto ToDto(PurchaseReceipt entity, IReadOnlyDictionary<Guid, ReceiptLineReturnState> returnStateByLineId)
    {
        return new PurchaseReceiptDto(
            entity.Id,
            entity.ReceiptNo,
            entity.SupplierId,
            entity.Supplier?.Code ?? string.Empty,
            entity.Supplier?.Name ?? string.Empty,
            entity.WarehouseId,
            entity.Warehouse?.Code ?? string.Empty,
            entity.Warehouse?.Name ?? string.Empty,
            entity.PurchaseOrderId,
            entity.PurchaseOrder?.PoNo,
            entity.ReceiptDate,
            entity.SupplierPayableAmount,
            entity.Notes,
            entity.Status,
            entity.ReversalDocumentId,
            entity.ReversedAt,
            entity.Lines
                .OrderBy(line => line.LineNo)
                .Select(line => new PurchaseReceiptLineDto(
                    line.Id,
                    line.LineNo,
                    line.PurchaseOrderLineId,
                    line.ItemId,
                    line.Item?.Code ?? string.Empty,
                    line.Item?.Name ?? string.Empty,
                    line.OrderedQtySnapshot,
                    line.ReceivedQty,
                    returnStateByLineId.TryGetValue(line.Id, out var returnState) ? returnState.ReturnedQty : 0m,
                    returnStateByLineId.TryGetValue(line.Id, out returnState) ? returnState.RemainingReturnableQty : 0m,
                    line.UomId,
                    line.Uom?.Code ?? string.Empty,
                    line.Uom?.Name ?? string.Empty,
                    line.Notes,
                    line.Components
                        .OrderBy(component => component.ComponentItem?.Code)
                        .Select(component => new PurchaseReceiptLineComponentDto(
                            component.Id,
                            component.ComponentItemId,
                            component.ComponentItem?.Code ?? string.Empty,
                            component.ComponentItem?.Name ?? string.Empty,
                            component.ExpectedQty,
                            component.ActualReceivedQty,
                            component.UomId,
                            component.Uom?.Code ?? string.Empty,
                            component.Uom?.Name ?? string.Empty,
                            component.ShortageReasonCodeId,
                            component.ShortageReasonCode?.Code,
                            component.ShortageReasonCode?.Name,
                            component.Notes,
                            component.CreatedAt,
                            component.UpdatedAt))
                        .ToArray(),
                    line.CreatedAt,
                    line.UpdatedAt))
                .ToArray(),
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static decimal ClampToZero(decimal value)
    {
        return Math.Abs(value) < 0.000001m ? 0m : Math.Max(value, 0m);
    }

    private sealed record ReceiptLineReturnState(decimal ReturnedQty, decimal RemainingReturnableQty);
}
