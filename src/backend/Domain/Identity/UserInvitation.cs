using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class UserInvitation : AuditableEntity, IOrganizationScopedEntity
{
    public Guid OrganizationId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public Guid? ProfileId { get; set; }

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? RevokedBy { get; set; }

    public AccessScopeMode BranchAccessMode { get; set; } = AccessScopeMode.All;

    public AccessScopeMode WarehouseAccessMode { get; set; } = AccessScopeMode.All;

    public ICollection<UserInvitationRole> Roles { get; set; } = new List<UserInvitationRole>();

    public ICollection<UserInvitationWarehouseAccess> WarehouseAccesses { get; set; } = new List<UserInvitationWarehouseAccess>();

    public ICollection<UserInvitationBranchAccess> BranchAccesses { get; set; } = new List<UserInvitationBranchAccess>();
}
