using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class AuditLogEntry : AuditableEntity, IOrganizationScopedEntity
{
    public Guid OrganizationId { get; set; }

    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public string? ResourceId { get; set; }

    public string? BeforeData { get; set; }

    public string? AfterData { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }
}
