using ERP.Application.Common.Pagination;

namespace ERP.Application.Inventory.Monitoring;

public enum InventoryStockStatus
{
    NotMonitored,
    Healthy,
    LowStock,
    OutOfStock
}

public sealed record InventoryMonitoringDashboardDto(
    int TotalMonitoredItems,
    int HealthyItems,
    int LowStockItems,
    int OutOfStockItems);

public sealed record InventoryMonitoringFilterOptionsDto(
    IReadOnlyList<InventoryMonitoringFilterOptionDto> Warehouses,
    IReadOnlyList<InventoryMonitoringFilterOptionDto> Categories);

public sealed record InventoryMonitoringFilterOptionDto(
    Guid Id,
    string Code,
    string Name);

public sealed record InventoryMonitoringListQuery(
    string? Search,
    Guid? WarehouseId,
    Guid? CategoryId,
    InventoryStockStatus? Status,
    bool OnlyMonitored,
    int Page,
    int PageSize,
    string? SortBy,
    SortDirection SortDirection);

public sealed record InventoryMonitoringItemDto(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid? CategoryId,
    string? CategoryCode,
    string? CategoryName,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid BaseUomId,
    string BaseUomCode,
    decimal CurrentStock,
    bool EnableMonitoring,
    decimal MinimumStock,
    decimal? ReorderPoint,
    decimal? MaximumStock,
    decimal? ReorderQuantity,
    decimal? SafetyStock,
    int? LeadTimeDays,
    decimal SuggestedReorderQuantity,
    InventoryStockStatus Status);

public sealed record ReorderSettingsDto(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid BaseUomId,
    string BaseUomCode,
    bool EnableMonitoring,
    decimal MinimumStock,
    decimal? ReorderPoint,
    decimal? MaximumStock,
    decimal? ReorderQuantity,
    decimal? SafetyStock,
    int? LeadTimeDays,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record UpdateReorderSettingsRequest(
    bool EnableMonitoring,
    decimal MinimumStock,
    decimal ReorderPoint,
    decimal MaximumStock,
    decimal ReorderQuantity,
    decimal? SafetyStock,
    int? LeadTimeDays);
