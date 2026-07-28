using ERP.Application.Common.Pagination;
using ERP.Domain.Common;
using ERP.Domain.Inventory;

namespace ERP.Application.Inventory.Adjustments;

public sealed record InventoryAdjustmentListQuery(
    string? Search,
    string? AdjustmentNo,
    Guid? WarehouseId,
    DocumentStatus? Status,
    DateTime? FromDate,
    DateTime? ToDate,
    string? Reason,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Desc);

public sealed record InventoryAdjustmentListItemDto(
    Guid Id,
    string AdjustmentNo,
    DateTime AdjustmentDate,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    DocumentStatus Status,
    string Reason,
    int LineCount,
    string CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PostedAt,
    string? PostedBy,
    DateTime? CanceledAt,
    string? CanceledBy);

public sealed record InventoryAdjustmentDto(
    Guid Id,
    string AdjustmentNo,
    DateTime AdjustmentDate,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    string Reason,
    string? Notes,
    DocumentStatus Status,
    IReadOnlyList<InventoryAdjustmentLineDto> Lines,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    DateTime? PostedAt,
    string? PostedBy,
    DateTime? CanceledAt,
    string? CanceledBy);

public sealed record InventoryAdjustmentLineDto(
    Guid Id,
    int LineNo,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid UomId,
    string UomCode,
    string UomName,
    decimal Quantity,
    InventoryAdjustmentType AdjustmentType,
    decimal BaseQty,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record UpsertInventoryAdjustmentRequest(
    string? AdjustmentNo,
    DateTime? AdjustmentDate,
    Guid WarehouseId,
    string Reason,
    string? Notes,
    IReadOnlyList<UpsertInventoryAdjustmentLineRequest> Lines);

public sealed record UpsertInventoryAdjustmentLineRequest(
    int LineNo,
    Guid ItemId,
    Guid UomId,
    decimal Quantity,
    InventoryAdjustmentType AdjustmentType,
    string? Notes);
