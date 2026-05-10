using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class RolePermission : AuditableEntity, IOrganizationScopedEntity
{
    public Guid OrganizationId { get; set; }

    public Guid RoleId { get; set; }

    public Role? Role { get; set; }

    public string PermissionKey { get; set; } = string.Empty;
}
