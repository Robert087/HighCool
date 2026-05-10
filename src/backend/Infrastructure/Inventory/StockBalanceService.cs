using ERP.Application.Common.Pagination;
using ERP.Application.Inventory;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Inventory;

public sealed class StockBalanceService(AppDbContext dbContext) : IStockBalanceService
{
    public async Task<PagedResult<StockBalanceDto>> ListAsync(StockBalanceQuery query, CancellationToken cancellationToken)
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

        if (dbContext.Database.IsSqlite())
        {
            var entries = await entriesQuery
                .Include(entity => entity.Item)
                    .ThenInclude(entity => entity!.BaseUom)
                .Include(entity => entity.Warehouse)
                .ToListAsync(cancellationToken);

            var inMemoryQuery = entries
                .GroupBy(entity => new
                {
                    entity.ItemId,
                    ItemCode = entity.Item!.Code,
                    ItemName = entity.Item.Name,
                    entity.WarehouseId,
                    WarehouseCode = entity.Warehouse!.Code,
                    WarehouseName = entity.Warehouse.Name,
                    entity.Item.BaseUomId,
                    BaseUomCode = entity.Item.BaseUom!.Code,
                    BaseUomName = entity.Item.BaseUom.Name
                })
                .Select(group => new StockBalanceDto(
                    group.Key.ItemId,
                    group.Key.ItemCode,
                    group.Key.ItemName,
                    group.Key.WarehouseId,
                    group.Key.WarehouseCode,
                    group.Key.WarehouseName,
                    group.Key.BaseUomId,
                    group.Key.BaseUomCode,
                    group.Key.BaseUomName,
                    group.Sum(entity => entity.QtyIn > 0m ? entity.BaseQty : -entity.BaseQty),
                    group.Max(entity => entity.TransactionDate)))
                .AsQueryable();

            inMemoryQuery = ApplySorting(inMemoryQuery, query);
            var totalCountInMemory = inMemoryQuery.Count();
            var itemsInMemory = inMemoryQuery
                .Skip(pagination.Skip)
                .Take(pagination.NormalizedPageSize)
                .ToList();

            return new PagedResult<StockBalanceDto>(
                itemsInMemory,
                pagination.NormalizedPage,
                pagination.NormalizedPageSize,
                totalCountInMemory,
                CalculateTotalPages(totalCountInMemory, pagination.NormalizedPageSize),
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

        var groupedQuery = entriesQuery
            .GroupBy(entity => new
            {
                entity.ItemId,
                ItemCode = entity.Item!.Code,
                ItemName = entity.Item.Name,
                entity.WarehouseId,
                WarehouseCode = entity.Warehouse!.Code,
                WarehouseName = entity.Warehouse.Name,
                entity.Item.BaseUomId,
                BaseUomCode = entity.Item.BaseUom!.Code,
                BaseUomName = entity.Item.BaseUom.Name
            })
            .Select(group => new StockBalanceDto(
                group.Key.ItemId,
                group.Key.ItemCode,
                group.Key.ItemName,
                group.Key.WarehouseId,
                group.Key.WarehouseCode,
                group.Key.WarehouseName,
                group.Key.BaseUomId,
                group.Key.BaseUomCode,
                group.Key.BaseUomName,
                group.Sum(entity => entity.QtyIn > 0m ? entity.BaseQty : -entity.BaseQty),
                group.Max(entity => entity.TransactionDate)));

        groupedQuery = ApplySorting(groupedQuery, query);

        var totalCount = await groupedQuery.CountAsync(cancellationToken);
        var items = await groupedQuery
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StockBalanceDto>(
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

    private static IQueryable<StockBalanceDto> ApplySorting(IQueryable<StockBalanceDto> query, StockBalanceQuery request)
    {
        var sortBy = ResolveSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("warehouseCode", true) => query.OrderBy(entity => entity.WarehouseCode).ThenBy(entity => entity.ItemCode),
            ("warehouseCode", false) => query.OrderByDescending(entity => entity.WarehouseCode).ThenByDescending(entity => entity.ItemCode),
            ("balanceQty", true) => query.OrderBy(entity => entity.BalanceQty).ThenBy(entity => entity.ItemCode),
            ("balanceQty", false) => query.OrderByDescending(entity => entity.BalanceQty).ThenByDescending(entity => entity.ItemCode),
            ("lastTransactionDate", true) => query.OrderBy(entity => entity.LastTransactionDate).ThenBy(entity => entity.ItemCode),
            ("lastTransactionDate", false) => query.OrderByDescending(entity => entity.LastTransactionDate).ThenByDescending(entity => entity.ItemCode),
            _ when ascending => query.OrderBy(entity => entity.ItemCode).ThenBy(entity => entity.WarehouseCode),
            _ => query.OrderByDescending(entity => entity.ItemCode).ThenByDescending(entity => entity.WarehouseCode)
        };
    }

    private static string ResolveSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? "itemCode" : sortBy.Trim();
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }
}
