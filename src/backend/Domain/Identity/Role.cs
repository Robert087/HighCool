using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class Role : AuditableEntity, IOrganizationScopedEntity
{
    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? TemplateKey { get; set; }

    public bool IsSystemRole { get; set; }

    public bool IsProtected { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();

    public ICollection<MembershipRole> Memberships { get; set; } = new List<MembershipRole>();
}
