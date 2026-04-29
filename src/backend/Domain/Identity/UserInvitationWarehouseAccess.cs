using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class UserInvitationWarehouseAccess : AuditableEntity, IOrganizationScopedEntity
{
    public Guid OrganizationId { get; set; }

    public Guid InvitationId { get; set; }

    public UserInvitation? Invitation { get; set; }

    public Guid WarehouseId { get; set; }
}
