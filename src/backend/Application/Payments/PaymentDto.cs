using ERP.Domain.Common;
using ERP.Domain.Payments;

namespace ERP.Application.Payments;

public sealed record PaymentDto(
    Guid Id,
    string PaymentNo,
    PaymentPartyType PartyType,
    Guid PartyId,
    string PartyCode,
    string PartyName,
    PaymentDirection Direction,
    decimal Amount,
    decimal AllocatedAmount,
    decimal UnallocatedAmount,
    DateTime PaymentDate,
    string? Currency,
    decimal? ExchangeRate,
    PaymentMethod PaymentMethod,
    string? ReferenceNote,
    string? Notes,
    DocumentStatus Status,
    Guid? ReversalDocumentId,
    DateTime? ReversedAt,
    IReadOnlyList<PaymentAllocationDto> Allocations,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
