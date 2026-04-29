using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class MembershipRole : AuditableEntity, IOrganizationScopedEntity
{
    public Guid OrganizationId { get; set; }

    public Guid MembershipId { get; set; }

    public OrganizationMembership? Membership { get; set; }

    public Guid RoleId { get; set; }

    public Role? Role { get; set; }
}
