using ERP.Application.Common.Pagination;
using ERP.Domain.Common;

namespace ERP.Application.Inventory.Counts;

public sealed record InventoryCountListQuery(
    string? Search,
    string? CountNo,
    Guid? WarehouseId,
    DocumentStatus? Status,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Desc);

public sealed record InventoryCountListItemDto(
    Guid Id,
    string CountNo,
    DateTime CountDate,
    DateTime? SnapshotAt,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    DocumentStatus Status,
    int LineCount,
    string CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PostedAt,
    string? PostedBy,
    DateTime? CanceledAt,
    string? CanceledBy);

public sealed record InventoryCountDto(
    Guid Id,
    string CountNo,
    DateTime CountDate,
    DateTime? SnapshotAt,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    string? Notes,
    DocumentStatus Status,
    IReadOnlyList<InventoryCountLineDto> Lines,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    DateTime? PostedAt,
    string? PostedBy,
    DateTime? CanceledAt,
    string? CanceledBy);

public sealed record InventoryCountLineDto(
    Guid Id,
    int LineNo,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid UomId,
    string UomCode,
    string UomName,
    decimal SystemQty,
    decimal CountedQty,
    decimal VarianceQty,
    decimal BaseSystemQty,
    decimal BaseCountedQty,
    decimal BaseVarianceQty,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record UpsertInventoryCountRequest(
    string? CountNo,
    DateTime? CountDate,
    Guid WarehouseId,
    string? Notes,
    IReadOnlyList<UpsertInventoryCountLineRequest> Lines);

public sealed record UpsertInventoryCountLineRequest(
    int LineNo,
    Guid ItemId,
    Guid UomId,
    decimal CountedQty,
    string? Notes);
