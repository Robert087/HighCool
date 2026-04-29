using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class MembershipWarehouseAccess : AuditableEntity, IOrganizationScopedEntity
{
    public Guid OrganizationId { get; set; }

    public Guid MembershipId { get; set; }

    public OrganizationMembership? Membership { get; set; }

    public Guid WarehouseId { get; set; }
}
