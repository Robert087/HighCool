using ERP.Domain.Payments;

namespace ERP.Application.Payments;

public sealed record PaymentAllocationDto(
    Guid Id,
    PaymentTargetDocumentType TargetDocType,
    Guid TargetDocId,
    Guid? TargetLineId,
    string TargetDocumentNo,
    DateTime TargetDocumentDate,
    decimal OriginalAmount,
    decimal AdjustedAmount,
    decimal NetAmount,
    decimal AlreadyAllocatedAmount,
    decimal OpenAmount,
    string Status,
    decimal AllocatedAmount,
    int AllocationOrder,
    DateTime CreatedAt,
    string CreatedBy);
