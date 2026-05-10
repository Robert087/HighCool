using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class OrganizationMembership : AuditableEntity, IOrganizationScopedEntity
{
    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }

    public UserAccount? User { get; set; }

    public MembershipStatus Status { get; set; } = MembershipStatus.Active;

    public Guid? ProfileId { get; set; }

    public UserProfile? Profile { get; set; }

    public bool IsOwner { get; set; }

    public AccessScopeMode BranchAccessMode { get; set; } = AccessScopeMode.All;

    public AccessScopeMode WarehouseAccessMode { get; set; } = AccessScopeMode.All;

    public ICollection<MembershipRole> Roles { get; set; } = new List<MembershipRole>();

    public ICollection<MembershipWarehouseAccess> WarehouseAccesses { get; set; } = new List<MembershipWarehouseAccess>();

    public ICollection<MembershipBranchAccess> BranchAccesses { get; set; } = new List<MembershipBranchAccess>();
}
