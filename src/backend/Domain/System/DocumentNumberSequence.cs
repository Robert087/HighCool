using ERP.Domain.Common;

namespace ERP.Domain.System;

public sealed class DocumentNumberSequence : OrganizationScopedAuditableEntity
{
    public string DocumentType { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public int NextValue { get; set; } = 1;

    public int PaddingLength { get; set; } = 6;

    public int Version { get; set; }
}
