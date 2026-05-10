using ERP.Domain.Common;

namespace ERP.Domain.Reversals;

public sealed class DocumentReversal : OrganizationScopedAuditableEntity
{
    public string ReversalNo { get; set; } = string.Empty;

    public BusinessDocumentType ReversedDocumentType { get; set; }

    public Guid ReversedDocumentId { get; set; }

    public DateTime ReversalDate { get; set; }

    public string ReversalReason { get; set; } = string.Empty;
}
