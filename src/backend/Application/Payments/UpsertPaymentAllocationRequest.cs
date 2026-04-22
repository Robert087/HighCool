using ERP.Domain.Payments;

namespace ERP.Application.Payments;

public sealed record UpsertPaymentAllocationRequest(
    PaymentTargetDocumentType TargetDocType,
    Guid TargetDocId,
    Guid? TargetLineId,
    decimal AllocatedAmount,
    int AllocationOrder);
