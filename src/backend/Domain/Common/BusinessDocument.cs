namespace ERP.Domain.Common;

public abstract class BusinessDocument : OrganizationScopedAuditableEntity, IHasDocumentStatus
{
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public DateTime? PostedAt { get; set; }

    public string? PostedBy { get; set; }

    public DateTime? CanceledAt { get; set; }

    public string? CanceledBy { get; set; }

    public Guid? ReversalDocumentId { get; set; }

    public DateTime? ReversedAt { get; set; }
}
