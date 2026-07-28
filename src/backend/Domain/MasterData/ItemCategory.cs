using ERP.Domain.Common;

namespace ERP.Domain.MasterData;

public sealed class ItemCategory : OrganizationScopedAuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
