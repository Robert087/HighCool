using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class UserInvitationBranchAccess : AuditableEntity, IOrganizationScopedEntity
{
    public Guid OrganizationId { get; set; }

    public Guid InvitationId { get; set; }

    public UserInvitation? Invitation { get; set; }

    public string BranchCode { get; set; } = string.Empty;
}
