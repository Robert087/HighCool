using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class MembershipBranchAccess : AuditableEntity, IOrganizationScopedEntity
{
    public Guid OrganizationId { get; set; }

    public Guid MembershipId { get; set; }

    public OrganizationMembership? Membership { get; set; }

    public string BranchCode { get; set; } = string.Empty;
}
