using ERP.Application.Common.Pagination;
using ERP.Domain.Common;
using ERP.Domain.Inventory;

namespace ERP.Application.Inventory.Transfers;

public sealed record InventoryTransferListQuery(
    string? Search,
    string? TransferNo,
    Guid? SourceWarehouseId,
    Guid? DestinationWarehouseId,
    DocumentStatus? Status,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Desc);

public sealed record InventoryTransferListItemDto(
    Guid Id,
    string TransferNo,
    DateTime TransferDate,
    Guid SourceWarehouseId,
    string SourceWarehouseCode,
    string SourceWarehouseName,
    Guid DestinationWarehouseId,
    string DestinationWarehouseCode,
    string DestinationWarehouseName,
    DocumentStatus Status,
    int LineCount,
    string CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PostedAt,
    string? PostedBy,
    DateTime? CanceledAt,
    string? CanceledBy);

public sealed record InventoryTransferDto(
    Guid Id,
    string TransferNo,
    DateTime TransferDate,
    Guid SourceWarehouseId,
    string SourceWarehouseCode,
    string SourceWarehouseName,
    Guid DestinationWarehouseId,
    string DestinationWarehouseCode,
    string DestinationWarehouseName,
    string? Notes,
    DocumentStatus Status,
    IReadOnlyList<InventoryTransferLineDto> Lines,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    DateTime? PostedAt,
    string? PostedBy,
    DateTime? CanceledAt,
    string? CanceledBy);

public sealed record InventoryTransferLineDto(
    Guid Id,
    int LineNo,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid UomId,
    string UomCode,
    string UomName,
    decimal Quantity,
    decimal BaseQty,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record UpsertInventoryTransferRequest(
    string? TransferNo,
    DateTime? TransferDate,
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    string? Notes,
    IReadOnlyList<UpsertInventoryTransferLineRequest> Lines);

public sealed record UpsertInventoryTransferLineRequest(
    int LineNo,
    Guid ItemId,
    Guid UomId,
    decimal Quantity,
    string? Notes);
