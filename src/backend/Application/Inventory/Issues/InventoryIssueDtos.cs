using ERP.Application.Common.Pagination;
using ERP.Domain.Common;
using ERP.Domain.Inventory;

namespace ERP.Application.Inventory.Issues;

public sealed record InventoryIssueListQuery(
    string? Search,
    string? IssueNo,
    Guid? WarehouseId,
    InventoryIssueReason? Reason,
    DocumentStatus? Status,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Desc);

public sealed record InventoryIssueListItemDto(
    Guid Id,
    string IssueNo,
    DateTime IssueDate,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    InventoryIssueReason Reason,
    string? ReferenceNo,
    string? RequestedBy,
    DocumentStatus Status,
    int LineCount,
    string CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PostedAt,
    string? PostedBy,
    DateTime? CanceledAt,
    string? CanceledBy);

public sealed record InventoryIssueDto(
    Guid Id,
    string IssueNo,
    DateTime IssueDate,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    InventoryIssueReason Reason,
    string? ReferenceNo,
    string? RequestedBy,
    string? Notes,
    DocumentStatus Status,
    IReadOnlyList<InventoryIssueLineDto> Lines,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    DateTime? PostedAt,
    string? PostedBy,
    DateTime? CanceledAt,
    string? CanceledBy);

public sealed record InventoryIssueLineDto(
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

public sealed record UpsertInventoryIssueRequest(
    string? IssueNo,
    DateTime? IssueDate,
    Guid WarehouseId,
    InventoryIssueReason? Reason,
    string? ReferenceNo,
    string? RequestedBy,
    string? Notes,
    IReadOnlyList<UpsertInventoryIssueLineRequest> Lines);

public sealed record UpsertInventoryIssueLineRequest(
    int LineNo,
    Guid ItemId,
    Guid UomId,
    decimal Quantity,
    string? Notes);
