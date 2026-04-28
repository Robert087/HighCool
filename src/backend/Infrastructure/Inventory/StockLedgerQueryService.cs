using ERP.Application.Common.Pagination;
using ERP.Application.Inventory;
using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Inventory;

public sealed class StockLedgerQueryService(AppDbContext dbContext) : IStockLedgerQueryService
{
    public async Task<PagedResult<StockLedgerEntryDto>> ListAsync(StockLedgerQuery query, CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);

        var entriesQuery = dbContext.StockLedgerEntries
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            entriesQuery = entriesQuery.Where(entity =>
                entity.Item!.Code.Contains(search) ||
                entity.Item.Name.Contains(search) ||
                entity.Warehouse!.Code.Contains(search) ||
                entity.Warehouse.Name.Contains(search));
        }

        if (query.ItemId.HasValue)
        {
            entriesQuery = entriesQuery.Where(entity => entity.ItemId == query.ItemId.Value);
        }

        if (query.WarehouseId.HasValue)
        {
            entriesQuery = entriesQuery.Where(entity => entity.WarehouseId == query.WarehouseId.Value);
        }

        if (query.TransactionType.HasValue)
        {
            entriesQuery = entriesQuery.Where(entity => entity.TransactionType == query.TransactionType.Value);
        }

        if (query.FromDate.HasValue)
        {
            entriesQuery = entriesQuery.Where(entity => entity.TransactionDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            entriesQuery = entriesQuery.Where(entity => entity.TransactionDate <= query.ToDate.Value);
        }

        entriesQuery = ApplySorting(entriesQuery, query);

        var totalCount = await entriesQuery.CountAsync(cancellationToken);
        var rows = await entriesQuery
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(entity => new StockLedgerListProjection(
                entity.Id,
                entity.ItemId,
                entity.Item!.Code,
                entity.Item.Name,
                entity.WarehouseId,
                entity.Warehouse!.Code,
                entity.Warehouse.Name,
                entity.TransactionType,
                entity.SourceDocType,
                entity.SourceDocId,
                entity.SourceLineId,
                entity.QtyIn,
                entity.QtyOut,
                entity.UomId,
                entity.Uom!.Code,
                entity.Uom.Name,
                entity.BaseQty,
                entity.RunningBalanceQty,
                entity.TransactionDate,
                entity.UnitCost,
                entity.TotalCost,
                entity.CreatedAt,
                entity.CreatedBy))
            .ToListAsync(cancellationToken);

        var purchaseReceiptIds = rows
            .Where(entity => entity.SourceDocType == SourceDocumentType.PurchaseReceipt)
            .Select(entity => entity.SourceDocId)
            .Distinct()
            .ToArray();

        var shortageResolutionIds = rows
            .Where(entity => entity.SourceDocType == SourceDocumentType.ShortageResolution)
            .Select(entity => entity.SourceDocId)
            .Distinct()
            .ToArray();

        var purchaseReturnIds = rows
            .Where(entity => entity.SourceDocType == SourceDocumentType.PurchaseReturn)
            .Select(entity => entity.SourceDocId)
            .Distinct()
            .ToArray();

        var reversalIds = rows
            .Where(entity => entity.SourceDocType == SourceDocumentType.DocumentReversal)
            .Select(entity => entity.SourceDocId)
            .Distinct()
            .ToArray();

        var purchaseReceiptNumbers = purchaseReceiptIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.PurchaseReceipts
                .AsNoTracking()
                .Where(entity => purchaseReceiptIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.ReceiptNo, cancellationToken);

        var shortageResolutionNumbers = shortageResolutionIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.ShortageResolutions
                .AsNoTracking()
                .Where(entity => shortageResolutionIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.ResolutionNo, cancellationToken);

        var purchaseReturnNumbers = purchaseReturnIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.PurchaseReturns
                .AsNoTracking()
                .Where(entity => purchaseReturnIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.ReturnNo, cancellationToken);

        var reversalNumbers = reversalIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.DocumentReversals
                .AsNoTracking()
                .Where(entity => reversalIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.ReversalNo, cancellationToken);

        var items = rows
            .Select(entity => new StockLedgerEntryDto(
                entity.Id,
                entity.ItemId,
                entity.ItemCode,
                entity.ItemName,
                entity.WarehouseId,
                entity.WarehouseCode,
                entity.WarehouseName,
                entity.TransactionType,
                entity.SourceDocType,
                entity.SourceDocId,
                entity.SourceLineId,
                ResolveSourceDocumentNo(entity.SourceDocType, entity.SourceDocId, purchaseReceiptNumbers, shortageResolutionNumbers, purchaseReturnNumbers, reversalNumbers),
                entity.QtyIn,
                entity.QtyOut,
                entity.UomId,
                entity.UomCode,
                entity.UomName,
                entity.BaseQty,
                entity.RunningBalanceQty,
                entity.TransactionDate,
                entity.UnitCost,
                entity.TotalCost,
                entity.CreatedAt,
                entity.CreatedBy))
            .ToArray();

        return new PagedResult<StockLedgerEntryDto>(
            items,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            CalculateTotalPages(totalCount, pagination.NormalizedPageSize),
            new
            {
                query.Search,
                query.ItemId,
                query.WarehouseId,
                query.TransactionType,
                query.FromDate,
                query.ToDate
            },
            new PagedSort(ResolveSortBy(query.SortBy), query.SortDirection));
    }

    private static IQueryable<Domain.Inventory.StockLedgerEntry> ApplySorting(IQueryable<Domain.Inventory.StockLedgerEntry> query, StockLedgerQuery request)
    {
        var sortBy = ResolveSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("itemCode", true) => query.OrderBy(entity => entity.Item!.Code).ThenBy(entity => entity.TransactionDate),
            ("itemCode", false) => query.OrderByDescending(entity => entity.Item!.Code).ThenByDescending(entity => entity.TransactionDate),
            ("warehouseCode", true) => query.OrderBy(entity => entity.Warehouse!.Code).ThenBy(entity => entity.TransactionDate),
            ("warehouseCode", false) => query.OrderByDescending(entity => entity.Warehouse!.Code).ThenByDescending(entity => entity.TransactionDate),
            ("transactionType", true) => query.OrderBy(entity => entity.TransactionType).ThenBy(entity => entity.TransactionDate),
            ("transactionType", false) => query.OrderByDescending(entity => entity.TransactionType).ThenByDescending(entity => entity.TransactionDate),
            _ when ascending => query.OrderBy(entity => entity.TransactionDate).ThenBy(entity => entity.CreatedAt).ThenBy(entity => entity.Id),
            _ => query.OrderByDescending(entity => entity.TransactionDate).ThenByDescending(entity => entity.CreatedAt).ThenByDescending(entity => entity.Id)
        };
    }

    private static string ResolveSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? "transactionDate" : sortBy.Trim();
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private static string ResolveSourceDocumentNo(
        SourceDocumentType sourceDocType,
        Guid sourceDocId,
        IReadOnlyDictionary<Guid, string> purchaseReceiptNumbers,
        IReadOnlyDictionary<Guid, string> shortageResolutionNumbers,
        IReadOnlyDictionary<Guid, string> purchaseReturnNumbers,
        IReadOnlyDictionary<Guid, string> reversalNumbers)
    {
        return sourceDocType switch
        {
            SourceDocumentType.PurchaseReceipt when purchaseReceiptNumbers.TryGetValue(sourceDocId, out var receiptNo) => receiptNo,
            SourceDocumentType.ShortageResolution when shortageResolutionNumbers.TryGetValue(sourceDocId, out var resolutionNo) => resolutionNo,
            SourceDocumentType.PurchaseReturn when purchaseReturnNumbers.TryGetValue(sourceDocId, out var returnNo) => returnNo,
            SourceDocumentType.DocumentReversal when reversalNumbers.TryGetValue(sourceDocId, out var reversalNo) => reversalNo,
            _ => sourceDocId.ToString()
        };
    }

    private sealed record StockLedgerListProjection(
        Guid Id,
        Guid ItemId,
        string ItemCode,
        string ItemName,
        Guid WarehouseId,
        string WarehouseCode,
        string WarehouseName,
        StockTransactionType TransactionType,
        SourceDocumentType SourceDocType,
        Guid SourceDocId,
        Guid? SourceLineId,
        decimal QtyIn,
        decimal QtyOut,
        Guid UomId,
        string UomCode,
        string UomName,
        decimal BaseQty,
        decimal RunningBalanceQty,
        DateTime TransactionDate,
        decimal? UnitCost,
        decimal? TotalCost,
        DateTime CreatedAt,
        string CreatedBy);
}
