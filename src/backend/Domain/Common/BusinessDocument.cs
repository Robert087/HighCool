namespace ERP.Domain.Common;

public abstract class BusinessDocument : AuditableEntity, IHasDocumentStatus
{
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
}
