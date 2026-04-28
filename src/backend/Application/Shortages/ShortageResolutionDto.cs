using ERP.Domain.Common;
using ERP.Domain.Shortages;

namespace ERP.Application.Shortages;

public sealed record ShortageResolutionDto(
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
    string? Notes,
    DocumentStatus Status,
    Guid? ReversalDocumentId,
    DateTime? ReversedAt,
    string? ApprovedBy,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    IReadOnlyList<ShortageResolutionAllocationDto> Allocations);
