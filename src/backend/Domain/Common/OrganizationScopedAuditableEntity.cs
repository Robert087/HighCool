namespace ERP.Domain.Common;

public abstract class OrganizationScopedAuditableEntity : AuditableEntity, IOrganizationScopedEntity
{
    public Guid OrganizationId { get; set; }
}
