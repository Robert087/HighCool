using ERP.Domain.Common;
using ERP.Domain.Shortages;

namespace ERP.Application.Shortages;

public sealed record ShortageResolutionListItemDto(
    Guid Id,
    string ResolutionNo,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    ShortageResolutionType ResolutionType,
    DateTime ResolutionDate,
    decimal? TotalQty,
    decimal? TotalAmount,
    string? Currency,
    DocumentStatus Status,
    int AllocationCount,
    Guid? ReversalDocumentId,
    DateTime? ReversedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
