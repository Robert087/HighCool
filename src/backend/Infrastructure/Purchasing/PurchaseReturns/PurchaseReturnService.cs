using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Application.Purchasing.PurchaseReturns;
using ERP.Domain.Common;
using ERP.Domain.Purchasing;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Purchasing.PurchaseReturns;

public sealed class PurchaseReturnService(
    AppDbContext dbContext,
    IQuantityConversionService quantityConversionService) : IPurchaseReturnService
{
    public async Task<PagedResult<PurchaseReturnListItemDto>> ListAsync(PurchaseReturnListQuery query, CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);

        var purchaseReturns = dbContext.PurchaseReturns
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var value = query.Search.Trim();
            purchaseReturns = purchaseReturns.Where(entity =>
                entity.ReturnNo.Contains(value) ||
                entity.Supplier!.Code.Contains(value) ||
                entity.Supplier.Name.Contains(value) ||
                (entity.ReferenceReceipt != null && entity.ReferenceReceipt.ReceiptNo.Contains(value)) ||
                (entity.Notes != null && entity.Notes.Contains(value)));
        }

        if (query.Status.HasValue)
        {
            purchaseReturns = purchaseReturns.Where(entity => entity.Status == query.Status.Value);
        }

        if (query.FromDate.HasValue)
        {
            purchaseReturns = purchaseReturns.Where(entity => entity.ReturnDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            purchaseReturns = purchaseReturns.Where(entity => entity.ReturnDate <= query.ToDate.Value);
        }

        var totalCount = await purchaseReturns.CountAsync(cancellationToken);
        var items = await ApplySorting(purchaseReturns, query)
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(entity => new PurchaseReturnListItemDto(
                entity.Id,
                entity.ReturnNo,
                entity.SupplierId,
                entity.Supplier!.Code,
                entity.Supplier.Name,
                entity.ReferenceReceiptId,
                entity.ReferenceReceipt != null ? entity.ReferenceReceipt.ReceiptNo : null,
                entity.ReturnDate,
                entity.Status,
                entity.Lines.Count,
                entity.ReversalDocumentId,
                entity.ReversedAt,
                entity.CreatedAt,
                entity.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<PurchaseReturnListItemDto>(
            items,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            CalculateTotalPages(totalCount, pagination.NormalizedPageSize),
            new
            {
                query.Search,
                query.Status,
                query.FromDate,
                query.ToDate
            },
            new PagedSort(ResolveSortBy(query.SortBy), query.SortDirection));
    }

    public async Task<PurchaseReturnDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.PurchaseReturns
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.Supplier)
            .Include(item => item.ReferenceReceipt)
            .Include(item => item.Lines.OrderBy(line => line.LineNo))
                .ThenInclude(line => line.Item)
            .Include(item => item.Lines)
                .ThenInclude(line => line.Component)
            .Include(item => item.Lines)
                .ThenInclude(line => line.Warehouse)
            .Include(item => item.Lines)
                .ThenInclude(line => line.Uom)
            .Include(item => item.Lines)
                .ThenInclude(line => line.ReferenceReceiptLine)
                    .ThenInclude(line => line!.Item)
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var remainingReturnableByLineId = await BuildRemainingReturnableByReferenceLineIdAsync(
            entity.Lines
                .Where(line => line.ReferenceReceiptLineId.HasValue && line.ReferenceReceiptLine is not null)
                .Select(line => line.ReferenceReceiptLine!)
                .DistinctBy(line => line.Id)
                .ToArray(),
            entity.Id,
            cancellationToken);

        return ToDto(entity, remainingReturnableByLineId);
    }

    public async Task<PurchaseReturnDto> CreateDraftAsync(UpsertPurchaseReturnRequest request, string actor, CancellationToken cancellationToken)
    {
        await ValidateRequestAsync(request, null, cancellationToken);

        var entity = new PurchaseReturn
        {
            ReturnNo = await ResolveReturnNoAsync(request.ReturnNo, null, cancellationToken),
            SupplierId = request.SupplierId,
            ReferenceReceiptId = request.ReferenceReceiptId,
            ReturnDate = request.ReturnDate!.Value,
            Notes = Normalize(request.Notes),
            Status = DocumentStatus.Draft,
            CreatedBy = actor
        };

        dbContext.PurchaseReturns.Add(entity);
        await AddLinesAsync(entity, request.Lines, actor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(entity.Id, cancellationToken);
    }

    public async Task<PurchaseReturnDto?> UpdateDraftAsync(Guid id, UpsertPurchaseReturnRequest request, string actor, CancellationToken cancellationToken)
    {
        var entity = await dbContext.PurchaseReturns
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        EnsureEditable(entity);
        await ValidateRequestAsync(request, id, cancellationToken);

        entity.ReturnNo = await ResolveReturnNoAsync(request.ReturnNo, id, cancellationToken);
        entity.SupplierId = request.SupplierId;
        entity.ReferenceReceiptId = request.ReferenceReceiptId;
        entity.ReturnDate = request.ReturnDate!.Value;
        entity.Notes = Normalize(request.Notes);
        entity.UpdatedBy = actor;

        dbContext.PurchaseReturnLines.RemoveRange(entity.Lines);
        entity.Lines.Clear();
        await AddLinesAsync(entity, request.Lines, actor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(entity.Id, cancellationToken);
    }

    private async Task ValidateRequestAsync(UpsertPurchaseReturnRequest request, Guid? currentId, CancellationToken cancellationToken)
    {
        var supplierExists = await dbContext.Suppliers.AnyAsync(entity => entity.Id == request.SupplierId && entity.IsActive, cancellationToken);
        if (!supplierExists)
        {
            throw new InvalidOperationException("Supplier was not found.");
        }

        if (request.ReferenceReceiptId.HasValue)
        {
            var receipt = await dbContext.PurchaseReceipts
                .AsNoTracking()
                .SingleOrDefaultAsync(entity => entity.Id == request.ReferenceReceiptId.Value, cancellationToken);

            if (receipt is null)
            {
                throw new InvalidOperationException("Reference receipt was not found.");
            }

            if (receipt.SupplierId != request.SupplierId)
            {
                throw new InvalidOperationException("Purchase return supplier must match the reference receipt supplier.");
            }

            if (receipt.ReversalDocumentId.HasValue)
            {
                throw new InvalidOperationException("A reversed purchase receipt cannot be used as a return reference.");
            }
        }

        var duplicateLineNumbers = request.Lines
            .GroupBy(entity => entity.LineNo)
            .Any(group => group.Count() > 1);

        if (duplicateLineNumbers)
        {
            throw new DuplicateEntityException("Line numbers must be unique inside the purchase return.");
        }

        var duplicateReferenceLineRows = request.Lines
            .Where(entity => entity.ReferenceReceiptLineId.HasValue)
            .GroupBy(entity => entity.ReferenceReceiptLineId!.Value)
            .Any(group => group.Count() > 1);

        if (duplicateReferenceLineRows)
        {
            throw new DuplicateEntityException("Duplicate purchase return receipt references are not allowed inside the same document.");
        }

        var duplicateManualRows = request.Lines
            .Where(entity => !entity.ReferenceReceiptLineId.HasValue)
            .GroupBy(entity => new { entity.ItemId, entity.ComponentId, entity.WarehouseId, entity.UomId })
            .Any(group => group.Count() > 1);

        if (duplicateManualRows)
        {
            throw new DuplicateEntityException("Duplicate purchase return rows are not allowed inside the same document.");
        }

        var itemIds = request.Lines.Select(entity => entity.ItemId)
            .Concat(request.Lines.Where(entity => entity.ComponentId.HasValue).Select(entity => entity.ComponentId!.Value))
            .Distinct()
            .ToArray();

        var warehouseIds = request.Lines.Select(entity => entity.WarehouseId).Distinct().ToArray();
        var uomIds = request.Lines.Select(entity => entity.UomId).Distinct().ToArray();
        var referenceLineIds = request.Lines.Where(entity => entity.ReferenceReceiptLineId.HasValue)
            .Select(entity => entity.ReferenceReceiptLineId!.Value)
            .Distinct()
            .ToArray();

        var itemCount = await dbContext.Items.CountAsync(entity => itemIds.Contains(entity.Id) && entity.IsActive, cancellationToken);
        if (itemCount != itemIds.Length)
        {
            throw new InvalidOperationException("One or more item references were not found.");
        }

        var warehouseCount = await dbContext.Warehouses.CountAsync(entity => warehouseIds.Contains(entity.Id) && entity.IsActive, cancellationToken);
        if (warehouseCount != warehouseIds.Length)
        {
            throw new InvalidOperationException("One or more warehouse references were not found.");
        }

        var uomCount = await dbContext.Uoms.CountAsync(entity => uomIds.Contains(entity.Id) && entity.IsActive, cancellationToken);
        if (uomCount != uomIds.Length)
        {
            throw new InvalidOperationException("One or more UOM references were not found.");
        }

        if (referenceLineIds.Length == 0 && !request.ReferenceReceiptId.HasValue)
        {
            return;
        }

        var referenceLines = await dbContext.PurchaseReceiptLines
            .AsNoTracking()
            .Include(entity => entity.PurchaseReceipt)
            .ToDictionaryAsync(entity => entity.Id, cancellationToken);

        foreach (var line in request.Lines.Where(item => item.ReferenceReceiptLineId.HasValue))
        {
            if (!referenceLines.TryGetValue(line.ReferenceReceiptLineId!.Value, out var referenceLine))
            {
                throw new InvalidOperationException("Reference receipt line was not found.");
            }

            if (request.ReferenceReceiptId.HasValue && referenceLine.PurchaseReceiptId != request.ReferenceReceiptId.Value)
            {
                throw new InvalidOperationException("Purchase return lines must belong to the selected reference receipt.");
            }

            if (referenceLine.PurchaseReceipt?.SupplierId != request.SupplierId)
            {
                throw new InvalidOperationException("Purchase return line receipt references must belong to the selected supplier.");
            }

            if (line.ItemId != referenceLine.ItemId)
            {
                throw new InvalidOperationException("Purchase return line item must match the reference receipt line item.");
            }
        }

        await ValidateRemainingReturnableQuantitiesAsync(request, currentId, cancellationToken);
    }

    private async Task AddLinesAsync(
        PurchaseReturn entity,
        IReadOnlyList<UpsertPurchaseReturnLineRequest> lines,
        string actor,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Items
            .AsNoTracking()
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        foreach (var line in lines.OrderBy(item => item.LineNo))
        {
            var item = items[line.ItemId];
            decimal baseQty;

            try
            {
                baseQty = await quantityConversionService.ConvertAsync(line.ReturnQty, line.UomId, item.BaseUomId, cancellationToken);
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains("global UOM conversion", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Purchase return line {line.LineNo} requires a global UOM conversion from the return UOM to the item base UOM.");
            }

            entity.Lines.Add(new PurchaseReturnLine
            {
                LineNo = line.LineNo,
                ItemId = line.ItemId,
                ComponentId = line.ComponentId,
                WarehouseId = line.WarehouseId,
                ReturnQty = Round(line.ReturnQty),
                UomId = line.UomId,
                BaseQty = Round(baseQty),
                ReferenceReceiptLineId = line.ReferenceReceiptLineId,
                CreatedBy = actor
            });
        }
    }

    private async Task<string> ResolveReturnNoAsync(string? requestedNo, Guid? currentId, CancellationToken cancellationToken)
    {
        var value = string.IsNullOrWhiteSpace(requestedNo)
            ? $"RTN-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
            : requestedNo.Trim();

        var exists = await dbContext.PurchaseReturns.AnyAsync(entity => entity.ReturnNo == value && entity.Id != currentId, cancellationToken);
        if (exists)
        {
            throw new DuplicateEntityException($"Purchase return number '{value}' already exists.");
        }

        return value;
    }

    private async Task<PurchaseReturnDto> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        return await GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Purchase return was not found after save.");
    }

    private async Task ValidateRemainingReturnableQuantitiesAsync(
        UpsertPurchaseReturnRequest request,
        Guid? currentId,
        CancellationToken cancellationToken)
    {
        var referenceLineRequests = request.Lines.Where(line => line.ReferenceReceiptLineId.HasValue).ToArray();
        if (referenceLineRequests.Length == 0)
        {
            return;
        }

        var referenceLineIds = referenceLineRequests
            .Select(line => line.ReferenceReceiptLineId!.Value)
            .Distinct()
            .ToArray();

        var referenceLines = await dbContext.PurchaseReceiptLines
            .AsNoTracking()
            .Include(line => line.PurchaseReceipt)
            .Include(line => line.Item)
            .Where(line =>
                referenceLineIds.Contains(line.Id) &&
                line.PurchaseReceipt!.Status == DocumentStatus.Posted &&
                line.PurchaseReceipt.ReversalDocumentId == null)
            .ToDictionaryAsync(line => line.Id, cancellationToken);

        var remainingReturnableByReferenceLineId = await BuildRemainingReturnableByReferenceLineIdAsync(referenceLines.Values.ToArray(), currentId, cancellationToken);
        var itemsById = await dbContext.Items
            .AsNoTracking()
            .Where(item => request.Lines.Select(line => line.ItemId).Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        foreach (var line in referenceLineRequests)
        {
            var item = itemsById[line.ItemId];
            decimal requestedBaseQty;

            try
            {
                requestedBaseQty = await quantityConversionService.ConvertAsync(line.ReturnQty, line.UomId, item.BaseUomId, cancellationToken);
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains("global UOM conversion", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Purchase return line {line.LineNo} requires a global UOM conversion from the return UOM to the item base UOM.");
            }

            if (!referenceLines.ContainsKey(line.ReferenceReceiptLineId!.Value))
            {
                throw new InvalidOperationException("Purchase returns can only reference posted, non-reversed receipt lines.");
            }

            var referenceLine = referenceLines[line.ReferenceReceiptLineId.Value];
            var remainingReturnableQty = remainingReturnableByReferenceLineId.TryGetValue(line.ReferenceReceiptLineId.Value, out var remainingQty)
                ? remainingQty
                : 0m;

            var remainingReturnableBaseQty = await quantityConversionService.ConvertAsync(
                remainingReturnableQty,
                referenceLine.UomId,
                item.BaseUomId,
                cancellationToken);

            if (Round(requestedBaseQty) > Round(remainingReturnableBaseQty))
            {
                throw new InvalidOperationException($"Purchase return line {line.LineNo} exceeds the remaining returnable quantity.");
            }
        }
    }

    private async Task<IReadOnlyDictionary<Guid, decimal>> BuildRemainingReturnableByReferenceLineIdAsync(
        IReadOnlyCollection<PurchaseReceiptLine> referenceLines,
        Guid? currentPurchaseReturnId,
        CancellationToken cancellationToken)
    {
        if (referenceLines.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var referenceLineIds = referenceLines.Select(line => line.Id).ToArray();

        var returnedRows = await dbContext.PurchaseReturnLines
            .AsNoTracking()
            .Where(line =>
                line.ReferenceReceiptLineId.HasValue &&
                referenceLineIds.Contains(line.ReferenceReceiptLineId.Value) &&
                line.PurchaseReturn!.Status == DocumentStatus.Posted &&
                line.PurchaseReturn.ReversalDocumentId == null &&
                (!currentPurchaseReturnId.HasValue || line.PurchaseReturnId != currentPurchaseReturnId.Value))
            .Select(line => new
            {
                ReferenceReceiptLineId = line.ReferenceReceiptLineId!.Value,
                line.BaseQty
            })
            .ToListAsync(cancellationToken);

        var returnedByReferenceLineId = returnedRows
            .GroupBy(line => line.ReferenceReceiptLineId)
            .ToDictionary(group => group.Key, group => Round(group.Sum(line => line.BaseQty)));

        var remainingByReferenceLineId = new Dictionary<Guid, decimal>();

        foreach (var referenceLine in referenceLines)
        {
            if (referenceLine.Item is null)
            {
                throw new InvalidOperationException("Purchase return line reference is missing item information required for returnable quantity conversion.");
            }

            var receivedBaseQty = await quantityConversionService.ConvertAsync(
                referenceLine.ReceivedQty,
                referenceLine.UomId,
                referenceLine.Item.BaseUomId,
                cancellationToken);

            var returnedBaseQty = returnedByReferenceLineId.TryGetValue(referenceLine.Id, out var returnedQty) ? returnedQty : 0m;
            var remainingBaseQty = ClampToZero(Round(receivedBaseQty - returnedBaseQty));
            remainingByReferenceLineId[referenceLine.Id] = await ConvertBaseQtyToDocumentQtyAsync(remainingBaseQty, referenceLine, cancellationToken);
        }

        return remainingByReferenceLineId;
    }

    private static PurchaseReturnDto ToDto(PurchaseReturn entity, IReadOnlyDictionary<Guid, decimal> remainingReturnableByReferenceLineId)
    {
        return new PurchaseReturnDto(
            entity.Id,
            entity.ReturnNo,
            entity.SupplierId,
            entity.Supplier?.Code ?? string.Empty,
            entity.Supplier?.Name ?? string.Empty,
            entity.ReferenceReceiptId,
            entity.ReferenceReceipt?.ReceiptNo,
            entity.ReturnDate,
            entity.Notes,
            entity.Status,
            entity.ReversalDocumentId,
            entity.ReversedAt,
            entity.Lines.OrderBy(line => line.LineNo).Select(line => new PurchaseReturnLineDto(
                line.Id,
                line.LineNo,
                line.ItemId,
                line.Item?.Code ?? string.Empty,
                line.Item?.Name ?? string.Empty,
                line.ComponentId,
                line.Component?.Code,
                line.Component?.Name,
                line.WarehouseId,
                line.Warehouse?.Code ?? string.Empty,
                line.Warehouse?.Name ?? string.Empty,
                line.ReturnQty,
                line.ReferenceReceiptLineId.HasValue && remainingReturnableByReferenceLineId.TryGetValue(line.ReferenceReceiptLineId.Value, out var remainingReturnableQty)
                    ? remainingReturnableQty
                    : 0m,
                line.UomId,
                line.Uom?.Code ?? string.Empty,
                line.Uom?.Name ?? string.Empty,
                line.BaseQty,
                line.ReferenceReceiptLineId,
                line.ReferenceReceiptLine?.LineNo,
                line.CreatedAt,
                line.UpdatedAt)).ToList(),
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static void EnsureEditable(PurchaseReturn entity)
    {
        if (entity.Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft purchase returns can be edited.");
        }
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private static decimal ClampToZero(decimal value)
    {
        return Math.Abs(value) < 0.000001m ? 0m : Math.Max(value, 0m);
    }

    private static IQueryable<PurchaseReturn> ApplySorting(IQueryable<PurchaseReturn> query, PurchaseReturnListQuery request)
    {
        var sortBy = ResolveSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("returnNo", true) => query.OrderBy(entity => entity.ReturnNo).ThenBy(entity => entity.Id),
            ("returnNo", false) => query.OrderByDescending(entity => entity.ReturnNo).ThenByDescending(entity => entity.Id),
            ("supplierName", true) => query.OrderBy(entity => entity.Supplier!.Name).ThenBy(entity => entity.ReturnDate),
            ("supplierName", false) => query.OrderByDescending(entity => entity.Supplier!.Name).ThenByDescending(entity => entity.ReturnDate),
            ("status", true) => query.OrderBy(entity => entity.Status).ThenBy(entity => entity.ReturnDate),
            ("status", false) => query.OrderByDescending(entity => entity.Status).ThenByDescending(entity => entity.ReturnDate),
            _ when ascending => query.OrderBy(entity => entity.ReturnDate).ThenBy(entity => entity.CreatedAt),
            _ => query.OrderByDescending(entity => entity.ReturnDate).ThenByDescending(entity => entity.CreatedAt)
        };
    }

    private static string ResolveSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? "returnDate" : sortBy.Trim();
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private async Task<decimal> ConvertBaseQtyToDocumentQtyAsync(
        decimal baseQty,
        PurchaseReceiptLine referenceLine,
        CancellationToken cancellationToken)
    {
        if (baseQty == 0m)
        {
            return 0m;
        }

        if (referenceLine.Item is null)
        {
            throw new InvalidOperationException("Purchase return line reference is missing item information required for quantity conversion.");
        }

        if (referenceLine.UomId == referenceLine.Item.BaseUomId)
        {
            return Round(baseQty);
        }

        var forwardConversion = await dbContext.UomConversions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entity => entity.FromUomId == referenceLine.UomId &&
                          entity.ToUomId == referenceLine.Item.BaseUomId &&
                          entity.IsActive,
                cancellationToken);

        if (forwardConversion is null || forwardConversion.Factor == 0m)
        {
            throw new InvalidOperationException("A required global UOM conversion could not be resolved.");
        }

        return Round(baseQty / forwardConversion.Factor);
    }
}
