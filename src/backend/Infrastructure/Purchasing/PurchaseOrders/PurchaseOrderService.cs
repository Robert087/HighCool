using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.Purchasing.PurchaseOrders;
using ERP.Domain.Common;
using ERP.Domain.Purchasing;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Purchasing.PurchaseOrders;

public sealed class PurchaseOrderService(AppDbContext dbContext) : IPurchaseOrderService
{
    public async Task<PagedResult<PurchaseOrderListItemDto>> ListAsync(PurchaseOrderListQuery query, CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);

        var purchaseOrders = dbContext.PurchaseOrders
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            purchaseOrders = purchaseOrders.Where(entity =>
                entity.PoNo.Contains(search) ||
                entity.Supplier!.Code.Contains(search) ||
                entity.Supplier.Name.Contains(search) ||
                (entity.Notes != null && entity.Notes.Contains(search)));
        }

        if (query.Status.HasValue)
        {
            purchaseOrders = purchaseOrders.Where(entity => entity.Status == query.Status.Value);
        }

        if (query.FromDate.HasValue)
        {
            purchaseOrders = purchaseOrders.Where(entity => entity.OrderDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            purchaseOrders = purchaseOrders.Where(entity => entity.OrderDate <= query.ToDate.Value);
        }

        var basicQuery = purchaseOrders.Select(entity => new PurchaseOrderListBasicProjection(
            entity.Id,
            entity.PoNo,
            entity.SupplierId,
            entity.Supplier!.Code,
            entity.Supplier.Name,
            entity.OrderDate,
            entity.ExpectedDate,
            entity.Status,
            entity.Lines.Count(),
            entity.CreatedAt,
            entity.UpdatedAt));

        IReadOnlyList<PurchaseOrderListProjection> rows;
        int totalCount;

        if (query.ReceiptProgressStatus.HasValue)
        {
            var candidateRows = await basicQuery.ToListAsync(cancellationToken);
            var summaries = await BuildListSummariesAsync(candidateRows, cancellationToken);
            var filteredRows = ApplyReceiptProgressFilter(summaries, query.ReceiptProgressStatus.Value);
            totalCount = filteredRows.Count;
            rows = ApplySorting(filteredRows, query)
                .Skip(pagination.Skip)
                .Take(pagination.NormalizedPageSize)
                .ToArray();
        }
        else
        {
            totalCount = await basicQuery.CountAsync(cancellationToken);
            var pageRows = await ApplySorting(purchaseOrders, query)
                .Skip(pagination.Skip)
                .Take(pagination.NormalizedPageSize)
                .Select(entity => new PurchaseOrderListBasicProjection(
                    entity.Id,
                    entity.PoNo,
                    entity.SupplierId,
                    entity.Supplier!.Code,
                    entity.Supplier.Name,
                    entity.OrderDate,
                    entity.ExpectedDate,
                    entity.Status,
                    entity.Lines.Count(),
                    entity.CreatedAt,
                    entity.UpdatedAt))
                .ToListAsync(cancellationToken);
            rows = await BuildListSummariesAsync(pageRows, cancellationToken);
        }

        var items = rows
            .Select(entity => new PurchaseOrderListItemDto(
                entity.Id,
                entity.PoNo,
                entity.SupplierId,
                entity.SupplierCode,
                entity.SupplierName,
                entity.OrderDate,
                entity.ExpectedDate,
                entity.Status,
                ResolveReceiptProgressStatus(entity.LineCount, entity.ReceivedLineCount, entity.FullyReceivedLineCount),
                entity.LineCount,
                entity.CreatedAt,
                entity.UpdatedAt))
            .ToArray();

        return new PagedResult<PurchaseOrderListItemDto>(
            items,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            CalculateTotalPages(totalCount, pagination.NormalizedPageSize),
            new
            {
                query.Search,
                query.Status,
                query.ReceiptProgressStatus,
                query.FromDate,
                query.ToDate
            },
            new PagedSort(ResolveSortBy(query.SortBy), query.SortDirection));
    }

    public async Task<PurchaseOrderDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.PurchaseOrders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(purchaseOrder => purchaseOrder.Supplier)
            .Include(purchaseOrder => purchaseOrder.Lines.OrderBy(line => line.LineNo))
                .ThenInclude(line => line.Item)
            .Include(purchaseOrder => purchaseOrder.Lines)
                .ThenInclude(line => line.Uom)
            .SingleOrDefaultAsync(purchaseOrder => purchaseOrder.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var receivedByLine = await GetPostedReceiptTotalsByPurchaseOrderLineAsync([entity.Id], cancellationToken);
        return ToDto(entity, receivedByLine);
    }

    public async Task<PurchaseOrderDto> CreateDraftAsync(UpsertPurchaseOrderRequest request, string actor, CancellationToken cancellationToken)
    {
        var poNo = await ResolvePoNoAsync(request.PoNo, null, cancellationToken);
        await ValidateDraftRequestAsync(request, null, cancellationToken);

        var purchaseOrder = new PurchaseOrder
        {
            PoNo = poNo,
            SupplierId = request.SupplierId,
            OrderDate = request.OrderDate!.Value,
            ExpectedDate = request.ExpectedDate,
            Notes = NormalizeOptionalText(request.Notes),
            Status = DocumentStatus.Draft,
            CreatedBy = actor
        };

        dbContext.PurchaseOrders.Add(purchaseOrder);
        AddLines(purchaseOrder, request.Lines, actor);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(purchaseOrder.Id, cancellationToken);
    }

    public async Task<PurchaseOrderDto?> UpdateDraftAsync(Guid id, UpsertPurchaseOrderRequest request, string actor, CancellationToken cancellationToken)
    {
        var purchaseOrder = await dbContext.PurchaseOrders
            .Include(entity => entity.Lines)
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (purchaseOrder is null)
        {
            return null;
        }

        EnsureDraftIsEditable(purchaseOrder);

        purchaseOrder.PoNo = await ResolvePoNoAsync(request.PoNo, id, cancellationToken);
        await ValidateDraftRequestAsync(request, id, cancellationToken);

        purchaseOrder.SupplierId = request.SupplierId;
        purchaseOrder.OrderDate = request.OrderDate!.Value;
        purchaseOrder.ExpectedDate = request.ExpectedDate;
        purchaseOrder.Notes = NormalizeOptionalText(request.Notes);
        purchaseOrder.UpdatedBy = actor;

        dbContext.PurchaseOrderLines.RemoveRange(purchaseOrder.Lines);
        AddLines(purchaseOrder, request.Lines, actor);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<PurchaseOrderAvailableLineDto>> ListAvailableLinesForReceiptAsync(Guid id, CancellationToken cancellationToken)
    {
        var purchaseOrder = await dbContext.PurchaseOrders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(entity => entity.Lines.OrderBy(line => line.LineNo))
                .ThenInclude(line => line.Item)
            .Include(entity => entity.Lines)
                .ThenInclude(line => line.Uom)
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (purchaseOrder is null)
        {
            return [];
        }

        if (purchaseOrder.Status != DocumentStatus.Posted)
        {
            throw new InvalidOperationException("Only posted purchase orders can be used to create receipts.");
        }

        var receivedByLine = await GetPostedReceiptTotalsByPurchaseOrderLineAsync([id], cancellationToken);

        return purchaseOrder.Lines
            .Select(line =>
            {
                var receivedQty = receivedByLine.TryGetValue(line.Id, out var total) ? total : 0m;
                var remainingQty = decimal.Max(0m, line.OrderedQty - receivedQty);
                return new PurchaseOrderAvailableLineDto(
                    line.Id,
                    line.LineNo,
                    line.ItemId,
                    line.Item?.Code ?? string.Empty,
                    line.Item?.Name ?? string.Empty,
                    line.OrderedQty,
                    line.UnitPrice,
                    receivedQty,
                    remainingQty,
                    line.UomId,
                    line.Uom?.Code ?? string.Empty,
                    line.Uom?.Name ?? string.Empty,
                    line.Notes);
            })
            .Where(line => line.RemainingQty > 0m)
            .ToArray();
    }

    private async Task ValidateDraftRequestAsync(UpsertPurchaseOrderRequest request, Guid? currentId, CancellationToken cancellationToken)
    {
        var supplierExists = await dbContext.Suppliers.AnyAsync(entity => entity.Id == request.SupplierId && entity.IsActive, cancellationToken);
        if (!supplierExists)
        {
            throw new InvalidOperationException("Supplier was not found.");
        }

        var itemIds = request.Lines.Select(line => line.ItemId).Distinct().ToArray();
        var uomIds = request.Lines.Select(line => line.UomId).Distinct().ToArray();

        var existingItems = await dbContext.Items
            .Where(entity => itemIds.Contains(entity.Id) && entity.IsActive)
            .Select(entity => entity.Id)
            .ToListAsync(cancellationToken);

        if (existingItems.Count != itemIds.Length)
        {
            throw new InvalidOperationException("One or more item references were not found.");
        }

        var existingUoms = await dbContext.Uoms
            .Where(entity => uomIds.Contains(entity.Id) && entity.IsActive)
            .Select(entity => entity.Id)
            .ToListAsync(cancellationToken);

        if (existingUoms.Count != uomIds.Length)
        {
            throw new InvalidOperationException("One or more UOM references were not found.");
        }

        var duplicateLineNumbers = request.Lines
            .GroupBy(line => line.LineNo)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateLineNumbers.Length > 0)
        {
            throw new DuplicateEntityException("Line numbers must be unique inside the purchase order.");
        }
    }

    private void AddLines(PurchaseOrder purchaseOrder, IReadOnlyList<UpsertPurchaseOrderLineRequest> lines, string actor)
    {
        foreach (var line in lines.OrderBy(entry => entry.LineNo))
        {
            purchaseOrder.Lines.Add(new PurchaseOrderLine
            {
                PurchaseOrder = purchaseOrder,
                LineNo = line.LineNo,
                ItemId = line.ItemId,
                OrderedQty = line.OrderedQty,
                UnitPrice = RoundAmount(line.UnitPrice),
                UomId = line.UomId,
                Notes = NormalizeOptionalText(line.Notes),
                CreatedBy = actor
            });
        }
    }

    private async Task<string> ResolvePoNoAsync(string? poNo, Guid? currentId, CancellationToken cancellationToken)
    {
        var value = string.IsNullOrWhiteSpace(poNo)
            ? $"PO-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
            : poNo.Trim();

        var exists = await dbContext.PurchaseOrders.AnyAsync(entity => entity.PoNo == value && entity.Id != currentId, cancellationToken);
        if (exists)
        {
            throw new DuplicateEntityException($"Purchase order number '{value}' already exists.");
        }

        return value;
    }

    private async Task<PurchaseOrderDto> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        var dto = await GetAsync(id, cancellationToken);
        return dto ?? throw new InvalidOperationException("Purchase order was not found after save.");
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static decimal RoundAmount(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private static void EnsureDraftIsEditable(PurchaseOrder purchaseOrder)
    {
        if (purchaseOrder.Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft purchase orders can be edited.");
        }
    }

    private async Task<Dictionary<Guid, decimal>> GetPostedReceiptTotalsByPurchaseOrderLineAsync(IReadOnlyCollection<Guid> purchaseOrderIds, CancellationToken cancellationToken)
    {
        if (purchaseOrderIds.Count == 0)
        {
            return [];
        }

        var postedLines = await dbContext.PurchaseReceiptLines
            .AsNoTracking()
            .Where(line =>
                line.PurchaseOrderLineId.HasValue &&
                purchaseOrderIds.Contains(line.PurchaseReceipt!.PurchaseOrderId!.Value) &&
                line.PurchaseReceipt.Status == DocumentStatus.Posted &&
                line.PurchaseReceipt.ReversalDocumentId == null)
            .Select(line => new
            {
                PurchaseOrderLineId = line.PurchaseOrderLineId!.Value,
                line.ReceivedQty
            })
            .ToListAsync(cancellationToken);

        return postedLines
            .GroupBy(line => line.PurchaseOrderLineId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.ReceivedQty));
    }

    private static PurchaseOrderReceiptProgressStatus ResolveReceiptProgressStatus(
        PurchaseOrder purchaseOrder,
        IReadOnlyDictionary<Guid, decimal> receivedByLine)
    {
        if (purchaseOrder.Status != DocumentStatus.Posted || purchaseOrder.Lines.Count == 0)
        {
            return PurchaseOrderReceiptProgressStatus.NotReceived;
        }

        var anyReceived = false;
        var allFullyReceived = true;

        foreach (var line in purchaseOrder.Lines)
        {
            var receivedQty = receivedByLine.TryGetValue(line.Id, out var total) ? total : 0m;
            if (receivedQty > 0m)
            {
                anyReceived = true;
            }

            if (receivedQty < line.OrderedQty)
            {
                allFullyReceived = false;
            }
        }

        if (allFullyReceived)
        {
            return PurchaseOrderReceiptProgressStatus.FullyReceived;
        }

        return anyReceived
            ? PurchaseOrderReceiptProgressStatus.PartiallyReceived
            : PurchaseOrderReceiptProgressStatus.NotReceived;
    }

    private static PurchaseOrderReceiptProgressStatus ResolveReceiptProgressStatus(int lineCount, int receivedLineCount, int fullyReceivedLineCount)
    {
        if (receivedLineCount <= 0)
        {
            return PurchaseOrderReceiptProgressStatus.NotReceived;
        }

        if (lineCount > 0 && fullyReceivedLineCount == lineCount)
        {
            return PurchaseOrderReceiptProgressStatus.FullyReceived;
        }

        return PurchaseOrderReceiptProgressStatus.PartiallyReceived;
    }

    private async Task<IReadOnlyList<PurchaseOrderListProjection>> BuildListSummariesAsync(
        IReadOnlyList<PurchaseOrderListBasicProjection> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var orderIds = rows.Select(entity => entity.Id).ToArray();
        var lines = await dbContext.PurchaseOrderLines
            .AsNoTracking()
            .Where(entity => orderIds.Contains(entity.PurchaseOrderId))
            .Select(entity => new PurchaseOrderLineSummary(entity.PurchaseOrderId, entity.Id, entity.OrderedQty))
            .ToListAsync(cancellationToken);

        var lineIds = lines.Select(entity => entity.LineId).ToArray();
        var receivedTotalsByLineId = lineIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : (await dbContext.PurchaseReceiptLines
                .AsNoTracking()
                .Where(entity =>
                    entity.PurchaseOrderLineId.HasValue &&
                    lineIds.Contains(entity.PurchaseOrderLineId.Value) &&
                    entity.PurchaseReceipt!.Status == DocumentStatus.Posted &&
                    entity.PurchaseReceipt.ReversalDocumentId == null)
                .Select(entity => new
                {
                    LineId = entity.PurchaseOrderLineId!.Value,
                    entity.ReceivedQty
                })
                .ToListAsync(cancellationToken))
                .GroupBy(entity => entity.LineId)
                .ToDictionary(group => group.Key, group => group.Sum(entity => entity.ReceivedQty));

        var lineSummariesByOrderId = lines
            .GroupBy(entity => entity.PurchaseOrderId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return rows.Select(entity =>
        {
            var orderLines = lineSummariesByOrderId.GetValueOrDefault(entity.Id, []);
            var receivedLineCount = orderLines.Count(line =>
                receivedTotalsByLineId.TryGetValue(line.LineId, out var total) && total > 0m);
            var fullyReceivedLineCount = orderLines.Count(line =>
                receivedTotalsByLineId.TryGetValue(line.LineId, out var total) && total >= line.OrderedQty);

            return new PurchaseOrderListProjection(
                entity.Id,
                entity.PoNo,
                entity.SupplierId,
                entity.SupplierCode,
                entity.SupplierName,
                entity.OrderDate,
                entity.ExpectedDate,
                entity.Status,
                entity.LineCount,
                receivedLineCount,
                fullyReceivedLineCount,
                entity.CreatedAt,
                entity.UpdatedAt);
        }).ToArray();
    }

    private static IReadOnlyList<PurchaseOrderListProjection> ApplyReceiptProgressFilter(
        IReadOnlyList<PurchaseOrderListProjection> rows,
        PurchaseOrderReceiptProgressStatus receiptProgressStatus)
    {
        return receiptProgressStatus switch
        {
            PurchaseOrderReceiptProgressStatus.NotReceived => rows.Where(entity => entity.ReceivedLineCount == 0).ToArray(),
            PurchaseOrderReceiptProgressStatus.PartiallyReceived => rows.Where(entity => entity.ReceivedLineCount > 0 && entity.FullyReceivedLineCount < entity.LineCount).ToArray(),
            PurchaseOrderReceiptProgressStatus.FullyReceived => rows.Where(entity => entity.LineCount > 0 && entity.FullyReceivedLineCount == entity.LineCount).ToArray(),
            _ => rows
        };
    }

    private static IQueryable<PurchaseOrder> ApplySorting(IQueryable<PurchaseOrder> query, PurchaseOrderListQuery request)
    {
        var sortBy = ResolveSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("poNo", true) => query.OrderBy(entity => entity.PoNo).ThenBy(entity => entity.Id),
            ("poNo", false) => query.OrderByDescending(entity => entity.PoNo).ThenByDescending(entity => entity.Id),
            ("supplierName", true) => query.OrderBy(entity => entity.Supplier!.Name).ThenBy(entity => entity.OrderDate),
            ("supplierName", false) => query.OrderByDescending(entity => entity.Supplier!.Name).ThenByDescending(entity => entity.OrderDate),
            ("status", true) => query.OrderBy(entity => entity.Status).ThenBy(entity => entity.OrderDate),
            ("status", false) => query.OrderByDescending(entity => entity.Status).ThenByDescending(entity => entity.OrderDate),
            _ when ascending => query.OrderBy(entity => entity.OrderDate).ThenBy(entity => entity.CreatedAt),
            _ => query.OrderByDescending(entity => entity.OrderDate).ThenByDescending(entity => entity.CreatedAt)
        };
    }

    private static IEnumerable<PurchaseOrderListProjection> ApplySorting(IEnumerable<PurchaseOrderListProjection> query, PurchaseOrderListQuery request)
    {
        var sortBy = ResolveSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("poNo", true) => query.OrderBy(entity => entity.PoNo).ThenBy(entity => entity.Id),
            ("poNo", false) => query.OrderByDescending(entity => entity.PoNo).ThenByDescending(entity => entity.Id),
            ("supplierName", true) => query.OrderBy(entity => entity.SupplierName).ThenBy(entity => entity.OrderDate),
            ("supplierName", false) => query.OrderByDescending(entity => entity.SupplierName).ThenByDescending(entity => entity.OrderDate),
            ("status", true) => query.OrderBy(entity => entity.Status).ThenBy(entity => entity.OrderDate),
            ("status", false) => query.OrderByDescending(entity => entity.Status).ThenByDescending(entity => entity.OrderDate),
            _ when ascending => query.OrderBy(entity => entity.OrderDate).ThenBy(entity => entity.CreatedAt),
            _ => query.OrderByDescending(entity => entity.OrderDate).ThenByDescending(entity => entity.CreatedAt)
        };
    }

    private static string ResolveSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? "orderDate" : sortBy.Trim();
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private static PurchaseOrderDto ToDto(PurchaseOrder entity, IReadOnlyDictionary<Guid, decimal> receivedByLine)
    {
        var lines = entity.Lines
            .OrderBy(line => line.LineNo)
            .Select(line =>
            {
                var receivedQty = receivedByLine.TryGetValue(line.Id, out var total) ? total : 0m;
                return new PurchaseOrderLineDto(
                    line.Id,
                    line.LineNo,
                    line.ItemId,
                    line.Item?.Code ?? string.Empty,
                    line.Item?.Name ?? string.Empty,
                    line.OrderedQty,
                    line.UnitPrice,
                    line.UomId,
                    line.Uom?.Code ?? string.Empty,
                    line.Uom?.Name ?? string.Empty,
                    receivedQty,
                    decimal.Max(0m, line.OrderedQty - receivedQty),
                    line.Notes,
                    line.CreatedAt,
                    line.UpdatedAt);
            })
            .ToArray();

        return new PurchaseOrderDto(
            entity.Id,
            entity.PoNo,
            entity.SupplierId,
            entity.Supplier?.Code ?? string.Empty,
            entity.Supplier?.Name ?? string.Empty,
            entity.OrderDate,
            entity.ExpectedDate,
            entity.Notes,
            entity.Status,
            ResolveReceiptProgressStatus(entity, receivedByLine),
            lines,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private sealed record PurchaseOrderListBasicProjection(
        Guid Id,
        string PoNo,
        Guid SupplierId,
        string SupplierCode,
        string SupplierName,
        DateTime OrderDate,
        DateTime? ExpectedDate,
        DocumentStatus Status,
        int LineCount,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    private sealed record PurchaseOrderLineSummary(
        Guid PurchaseOrderId,
        Guid LineId,
        decimal OrderedQty);

    private sealed record PurchaseOrderListProjection(
        Guid Id,
        string PoNo,
        Guid SupplierId,
        string SupplierCode,
        string SupplierName,
        DateTime OrderDate,
        DateTime? ExpectedDate,
        DocumentStatus Status,
        int LineCount,
        int ReceivedLineCount,
        int FullyReceivedLineCount,
        DateTime CreatedAt,
        DateTime? UpdatedAt);
}
