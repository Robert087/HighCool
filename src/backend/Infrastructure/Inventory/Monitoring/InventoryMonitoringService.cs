using ERP.Application.Common.Pagination;
using ERP.Application.Inventory.Monitoring;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Inventory.Monitoring;

public sealed class InventoryMonitoringService(AppDbContext dbContext) : IInventoryMonitoringService
{
    public async Task<InventoryMonitoringDashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var query = BuildMonitoringQuery().Where(row => row.EnableMonitoring);

        var total = await query.CountAsync(cancellationToken);
        var healthy = await query
            .Where(row => row.CurrentStock > row.ReorderPoint)
            .CountAsync(cancellationToken);
        var lowStock = await query
            .Where(row => row.CurrentStock > 0d && row.CurrentStock <= row.ReorderPoint)
            .CountAsync(cancellationToken);
        var outOfStock = await query
            .Where(row => row.CurrentStock <= 0d)
            .CountAsync(cancellationToken);

        return new InventoryMonitoringDashboardDto(total, healthy, lowStock, outOfStock);
    }

    public async Task<InventoryMonitoringFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        var warehouses = await dbContext.Warehouses
            .AsNoTracking()
            .Where(entity => entity.IsActive)
            .OrderBy(entity => entity.Code)
            .Select(entity => new InventoryMonitoringFilterOptionDto(entity.Id, entity.Code, entity.Name))
            .ToArrayAsync(cancellationToken);

        var categories = await dbContext.ItemCategories
            .AsNoTracking()
            .Where(entity => entity.IsActive)
            .OrderBy(entity => entity.Code)
            .Select(entity => new InventoryMonitoringFilterOptionDto(entity.Id, entity.Code, entity.Name))
            .ToArrayAsync(cancellationToken);

        return new InventoryMonitoringFilterOptionsDto(warehouses, categories);
    }

    public async Task<PagedResult<InventoryMonitoringItemDto>> ListItemsAsync(
        InventoryMonitoringListQuery query,
        CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var rowsQuery = BuildMonitoringQuery();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rowsQuery = rowsQuery.Where(row =>
                row.ItemCode.Contains(search) ||
                row.ItemName.Contains(search) ||
                row.WarehouseCode.Contains(search) ||
                row.WarehouseName.Contains(search) ||
                (row.CategoryCode != null && row.CategoryCode.Contains(search)) ||
                (row.CategoryName != null && row.CategoryName.Contains(search)));
        }

        if (query.WarehouseId.HasValue)
        {
            rowsQuery = rowsQuery.Where(row => row.WarehouseId == query.WarehouseId.Value);
        }

        if (query.CategoryId.HasValue)
        {
            rowsQuery = rowsQuery.Where(row => row.CategoryId == query.CategoryId.Value);
        }

        if (query.OnlyMonitored)
        {
            rowsQuery = rowsQuery.Where(row => row.EnableMonitoring);
        }

        if (query.Status.HasValue)
        {
            rowsQuery = ApplyStatusFilter(rowsQuery, query.Status.Value);
        }

        rowsQuery = ApplySorting(rowsQuery, query);

        var totalCount = await rowsQuery.CountAsync(cancellationToken);
        var rows = await rowsQuery
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(ToDto).ToArray();

        return new PagedResult<InventoryMonitoringItemDto>(
            items,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            CalculateTotalPages(totalCount, pagination.NormalizedPageSize),
            new
            {
                query.Search,
                query.WarehouseId,
                query.CategoryId,
                query.Status,
                query.OnlyMonitored
            },
            new PagedSort(ResolveSortBy(query.SortBy), query.SortDirection));
    }

    public Task<ReorderSettingsDto?> GetReorderSettingsAsync(Guid itemId, CancellationToken cancellationToken)
    {
        return dbContext.Items
            .AsNoTracking()
            .Where(entity => entity.Id == itemId)
            .Select(entity => new ReorderSettingsDto(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.BaseUomId,
                entity.BaseUom!.Code,
                entity.EnableInventoryMonitoring,
                entity.MinimumStockQuantity,
                entity.ReorderPointQuantity,
                entity.MaximumStockQuantity,
                entity.ReorderQuantity,
                entity.SafetyStockQuantity,
                entity.LeadTimeDays,
                entity.CreatedAt,
                entity.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ReorderSettingsDto?> UpdateReorderSettingsAsync(
        Guid itemId,
        UpdateReorderSettingsRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Items.SingleOrDefaultAsync(entity => entity.Id == itemId, cancellationToken);

        if (item is null)
        {
            return null;
        }

        item.EnableInventoryMonitoring = request.EnableMonitoring;
        item.MinimumStockQuantity = Round(request.MinimumStock);
        item.ReorderPointQuantity = Round(request.ReorderPoint);
        item.MaximumStockQuantity = Round(request.MaximumStock);
        item.ReorderQuantity = Round(request.ReorderQuantity);
        item.SafetyStockQuantity = request.SafetyStock.HasValue ? Round(request.SafetyStock.Value) : null;
        item.LeadTimeDays = request.LeadTimeDays;
        item.UpdatedBy = actor;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetReorderSettingsAsync(item.Id, cancellationToken);
    }

    private IQueryable<MonitoringProjection> BuildMonitoringQuery()
    {
        var stockQuantities = dbContext.StockLedgerEntries
            .AsNoTracking()
            .GroupBy(entry => new { entry.ItemId, entry.WarehouseId })
            .Select(group => new
            {
                group.Key.ItemId,
                group.Key.WarehouseId,
                CurrentStock = (double?)group.Sum(entry => entry.QtyIn > 0m ? (double)entry.BaseQty : -(double)entry.BaseQty)
            });

        return
            from item in dbContext.Items.AsNoTracking()
            from warehouse in dbContext.Warehouses.AsNoTracking()
            join stock in stockQuantities
                on new { ItemId = item.Id, WarehouseId = warehouse.Id }
                equals new { stock.ItemId, stock.WarehouseId }
                into stockJoin
            from stock in stockJoin.DefaultIfEmpty()
            where item.IsActive && warehouse.IsActive
            select new MonitoringProjection
            {
                ItemId = item.Id,
                ItemCode = item.Code,
                ItemName = item.Name,
                CategoryId = item.CategoryId,
                CategoryCode = item.Category != null ? item.Category.Code : null,
                CategoryName = item.Category != null ? item.Category.Name : null,
                WarehouseId = warehouse.Id,
                WarehouseCode = warehouse.Code,
                WarehouseName = warehouse.Name,
                BaseUomId = item.BaseUomId,
                BaseUomCode = item.BaseUom!.Code,
                CurrentStock = stock.CurrentStock ?? 0d,
                EnableMonitoring = item.EnableInventoryMonitoring,
                MinimumStock = (double)item.MinimumStockQuantity,
                HasReorderPoint = item.ReorderPointQuantity.HasValue,
                ReorderPoint = (double)(item.ReorderPointQuantity ?? 0m),
                HasMaximumStock = item.MaximumStockQuantity.HasValue,
                MaximumStock = (double)(item.MaximumStockQuantity ?? 0m),
                HasReorderQuantity = item.ReorderQuantity.HasValue,
                ReorderQuantity = (double)(item.ReorderQuantity ?? 0m),
                HasSafetyStock = item.SafetyStockQuantity.HasValue,
                SafetyStock = (double)(item.SafetyStockQuantity ?? 0m),
                LeadTimeDays = item.LeadTimeDays
            };
    }

    private static IQueryable<MonitoringProjection> ApplyStatusFilter(
        IQueryable<MonitoringProjection> query,
        InventoryStockStatus status)
    {
        return status switch
        {
            InventoryStockStatus.NotMonitored => query.Where(row => !row.EnableMonitoring),
            InventoryStockStatus.OutOfStock => query.Where(row => row.EnableMonitoring && row.CurrentStock <= 0d),
            InventoryStockStatus.LowStock => query.Where(row => row.EnableMonitoring && row.CurrentStock > 0d && row.CurrentStock <= row.ReorderPoint),
            InventoryStockStatus.Healthy => query.Where(row => row.EnableMonitoring && row.CurrentStock > row.ReorderPoint),
            _ => query
        };
    }

    private static IQueryable<MonitoringProjection> ApplySorting(
        IQueryable<MonitoringProjection> query,
        InventoryMonitoringListQuery request)
    {
        var sortBy = ResolveSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("itemCode", true) => query.OrderBy(row => row.ItemCode).ThenBy(row => row.WarehouseCode),
            ("itemCode", false) => query.OrderByDescending(row => row.ItemCode).ThenByDescending(row => row.WarehouseCode),
            ("warehouseCode", true) => query.OrderBy(row => row.WarehouseCode).ThenBy(row => row.ItemCode),
            ("warehouseCode", false) => query.OrderByDescending(row => row.WarehouseCode).ThenByDescending(row => row.ItemCode),
            ("currentStock", true) => query.OrderBy(row => row.CurrentStock).ThenBy(row => row.ItemCode).ThenBy(row => row.WarehouseCode),
            ("currentStock", false) => query.OrderByDescending(row => row.CurrentStock).ThenByDescending(row => row.ItemCode).ThenByDescending(row => row.WarehouseCode),
            ("minimumStock", true) => query.OrderBy(row => row.MinimumStock).ThenBy(row => row.ItemCode),
            ("minimumStock", false) => query.OrderByDescending(row => row.MinimumStock).ThenByDescending(row => row.ItemCode),
            ("reorderPoint", true) => query.OrderBy(row => row.ReorderPoint).ThenBy(row => row.ItemCode),
            ("reorderPoint", false) => query.OrderByDescending(row => row.ReorderPoint).ThenByDescending(row => row.ItemCode),
            ("maximumStock", true) => query.OrderBy(row => row.MaximumStock).ThenBy(row => row.ItemCode),
            ("maximumStock", false) => query.OrderByDescending(row => row.MaximumStock).ThenByDescending(row => row.ItemCode),
            ("suggestedReorderQuantity", true) => query.OrderBy(row => !row.HasMaximumStock || row.CurrentStock >= row.MaximumStock ? 0d : row.MaximumStock - row.CurrentStock).ThenBy(row => row.ItemCode),
            ("suggestedReorderQuantity", false) => query.OrderByDescending(row => !row.HasMaximumStock || row.CurrentStock >= row.MaximumStock ? 0d : row.MaximumStock - row.CurrentStock).ThenByDescending(row => row.ItemCode),
            ("status", true) => query.OrderBy(row => row.EnableMonitoring ? row.CurrentStock <= 0d ? 3 : row.CurrentStock <= row.ReorderPoint ? 2 : 1 : 0).ThenBy(row => row.ItemCode),
            ("status", false) => query.OrderByDescending(row => row.EnableMonitoring ? row.CurrentStock <= 0d ? 3 : row.CurrentStock <= row.ReorderPoint ? 2 : 1 : 0).ThenByDescending(row => row.ItemCode),
            _ when ascending => query.OrderBy(row => row.ItemName).ThenBy(row => row.WarehouseName),
            _ => query.OrderByDescending(row => row.ItemName).ThenByDescending(row => row.WarehouseName)
        };
    }

    private static InventoryMonitoringItemDto ToDto(MonitoringProjection row)
    {
        var currentStock = Round((decimal)row.CurrentStock);
        var maximumStock = row.HasMaximumStock ? Round((decimal)row.MaximumStock) : (decimal?)null;
        var suggested = maximumStock.HasValue && currentStock < maximumStock.Value
            ? Round(maximumStock.Value - currentStock)
            : 0m;

        return new InventoryMonitoringItemDto(
            row.ItemId,
            row.ItemCode,
            row.ItemName,
            row.CategoryId,
            row.CategoryCode,
            row.CategoryName,
            row.WarehouseId,
            row.WarehouseCode,
            row.WarehouseName,
            row.BaseUomId,
            row.BaseUomCode,
            currentStock,
            row.EnableMonitoring,
            Round((decimal)row.MinimumStock),
            row.HasReorderPoint ? Round((decimal)row.ReorderPoint) : null,
            maximumStock,
            row.HasReorderQuantity ? Round((decimal)row.ReorderQuantity) : null,
            row.HasSafetyStock ? Round((decimal)row.SafetyStock) : null,
            row.LeadTimeDays,
            suggested,
            ResolveStatus(row));
    }

    private static InventoryStockStatus ResolveStatus(MonitoringProjection row)
    {
        if (!row.EnableMonitoring)
        {
            return InventoryStockStatus.NotMonitored;
        }

        if (row.CurrentStock <= 0d)
        {
            return InventoryStockStatus.OutOfStock;
        }

        return row.CurrentStock <= row.ReorderPoint
            ? InventoryStockStatus.LowStock
            : InventoryStockStatus.Healthy;
    }

    private static string ResolveSortBy(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            "itemCode" => "itemCode",
            "warehouseCode" => "warehouseCode",
            "currentStock" => "currentStock",
            "minimumStock" => "minimumStock",
            "reorderPoint" => "reorderPoint",
            "maximumStock" => "maximumStock",
            "suggestedReorderQuantity" => "suggestedReorderQuantity",
            "status" => "status",
            _ => "itemName"
        };
    }

    private static decimal Round(decimal value)
    {
        return Math.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private sealed class MonitoringProjection
    {
        public Guid ItemId { get; init; }

        public string ItemCode { get; init; } = string.Empty;

        public string ItemName { get; init; } = string.Empty;

        public Guid? CategoryId { get; init; }

        public string? CategoryCode { get; init; }

        public string? CategoryName { get; init; }

        public Guid WarehouseId { get; init; }

        public string WarehouseCode { get; init; } = string.Empty;

        public string WarehouseName { get; init; } = string.Empty;

        public Guid BaseUomId { get; init; }

        public string BaseUomCode { get; init; } = string.Empty;

        public double CurrentStock { get; init; }

        public bool EnableMonitoring { get; init; }

        public double MinimumStock { get; init; }

        public bool HasReorderPoint { get; init; }

        public double ReorderPoint { get; init; }

        public bool HasMaximumStock { get; init; }

        public double MaximumStock { get; init; }

        public bool HasReorderQuantity { get; init; }

        public double ReorderQuantity { get; init; }

        public bool HasSafetyStock { get; init; }

        public double SafetyStock { get; init; }

        public int? LeadTimeDays { get; init; }
    }
}
