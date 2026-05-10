using ERP.Domain.Common;
using ERP.Domain.Payments;

namespace ERP.Application.Payments;

public sealed record PaymentListItemDto(
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
    PaymentMethod PaymentMethod,
    string? ReferenceNote,
    DocumentStatus Status,
    int AllocationCount,
    Guid? ReversalDocumentId,
    DateTime? ReversedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
