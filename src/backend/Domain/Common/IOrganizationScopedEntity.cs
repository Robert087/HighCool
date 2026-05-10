namespace ERP.Domain.Common;

public interface IOrganizationScopedEntity
{
    Guid OrganizationId { get; set; }
}
