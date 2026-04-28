namespace ERP.Domain.Common;

public abstract class BusinessDocument : AuditableEntity, IHasDocumentStatus
{
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public Guid? ReversalDocumentId { get; set; }

    public DateTime? ReversedAt { get; set; }
}
