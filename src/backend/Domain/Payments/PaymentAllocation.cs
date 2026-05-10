using ERP.Domain.Common;

namespace ERP.Domain.Payments;

public sealed class PaymentAllocation : OrganizationScopedAuditableEntity
{
    public Guid PaymentId { get; set; }

    public Payment? Payment { get; set; }

    public PaymentTargetDocumentType TargetDocType { get; set; }

    public Guid TargetDocId { get; set; }

    public Guid? TargetLineId { get; set; }

    public decimal AllocatedAmount { get; set; }

    public int AllocationOrder { get; set; }
}
