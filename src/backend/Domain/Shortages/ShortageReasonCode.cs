using ERP.Domain.Common;

namespace ERP.Domain.Shortages;

public sealed class ShortageReasonCode : OrganizationScopedAuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool AffectsSupplierBalance { get; set; }

    public bool AffectsStock { get; set; }

    public bool RequiresApproval { get; set; }

    public bool IsActive { get; set; } = true;
}
