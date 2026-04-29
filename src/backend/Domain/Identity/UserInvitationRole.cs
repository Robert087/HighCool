using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class UserInvitationRole : AuditableEntity, IOrganizationScopedEntity
{
    public Guid OrganizationId { get; set; }

    public Guid InvitationId { get; set; }

    public UserInvitation? Invitation { get; set; }

    public Guid RoleId { get; set; }

    public Role? Role { get; set; }
}
