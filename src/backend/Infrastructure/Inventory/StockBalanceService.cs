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
            return await ListSqliteAsync(entriesQuery, query, pagination, cancellationToken);
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

    private async Task<PagedResult<StockBalanceDto>> ListSqliteAsync(
        IQueryable<Domain.Inventory.StockLedgerEntry> entriesQuery,
        StockBalanceQuery query,
        PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        var groupedEntries = entriesQuery
            .GroupBy(entity => new
            {
                entity.ItemId,
                entity.WarehouseId
            })
            .Select(group => new
            {
                group.Key.ItemId,
                group.Key.WarehouseId,
                BalanceQty = group.Sum(entity => entity.QtyIn > 0m ? (double)entity.BaseQty : -(double)entity.BaseQty),
                LastTransactionDate = group.Max(entity => entity.TransactionDate)
            });

        var groupedQuery =
            from balance in groupedEntries
            join item in dbContext.Items.AsNoTracking() on balance.ItemId equals item.Id
            join warehouse in dbContext.Warehouses.AsNoTracking() on balance.WarehouseId equals warehouse.Id
            join baseUom in dbContext.Uoms.AsNoTracking() on item.BaseUomId equals baseUom.Id
            select new StockBalanceSqliteProjection
            {
                ItemId = balance.ItemId,
                ItemCode = item.Code,
                ItemName = item.Name,
                WarehouseId = balance.WarehouseId,
                WarehouseCode = warehouse.Code,
                WarehouseName = warehouse.Name,
                BaseUomId = item.BaseUomId,
                BaseUomCode = baseUom.Code,
                BaseUomName = baseUom.Name,
                BalanceQty = balance.BalanceQty,
                LastTransactionDate = balance.LastTransactionDate
            };

        groupedQuery = ApplySorting(groupedQuery, query);

        var totalCount = await groupedQuery.CountAsync(cancellationToken);
        var pageRows = await groupedQuery
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        var items = pageRows
            .Select(entity => new StockBalanceDto(
                entity.ItemId,
                entity.ItemCode,
                entity.ItemName,
                entity.WarehouseId,
                entity.WarehouseCode,
                entity.WarehouseName,
                entity.BaseUomId,
                entity.BaseUomCode,
                entity.BaseUomName,
                decimal.Round((decimal)entity.BalanceQty, 6, MidpointRounding.AwayFromZero),
                entity.LastTransactionDate))
            .ToList();

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

    private static IQueryable<StockBalanceSqliteProjection> ApplySorting(IQueryable<StockBalanceSqliteProjection> query, StockBalanceQuery request)
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

    private sealed class StockBalanceSqliteProjection
    {
        public Guid ItemId { get; init; }
        public string ItemCode { get; init; } = string.Empty;
        public string ItemName { get; init; } = string.Empty;
        public Guid WarehouseId { get; init; }
        public string WarehouseCode { get; init; } = string.Empty;
        public string WarehouseName { get; init; } = string.Empty;
        public Guid BaseUomId { get; init; }
        public string BaseUomCode { get; init; } = string.Empty;
        public string BaseUomName { get; init; } = string.Empty;
        public double BalanceQty { get; init; }
        public DateTime LastTransactionDate { get; init; }
    }
}
