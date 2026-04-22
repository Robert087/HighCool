using ERP.Domain.Payments;

namespace ERP.Application.Payments;

public sealed record UpsertPaymentRequest(
    string? PaymentNo,
    PaymentPartyType PartyType,
    Guid PartyId,
    PaymentDirection Direction,
    decimal Amount,
    DateTime? PaymentDate,
    string? Currency,
    decimal? ExchangeRate,
    PaymentMethod PaymentMethod,
    string? ReferenceNote,
    string? Notes,
    IReadOnlyList<UpsertPaymentAllocationRequest> Allocations);
