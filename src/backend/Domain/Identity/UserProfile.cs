using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class UserProfile : AuditableEntity, IOrganizationScopedEntity
{
    public Guid OrganizationId { get; set; }

    public string? JobTitle { get; set; }

    public string? Department { get; set; }

    public string? Phone { get; set; }

    public string? DefaultBranchCode { get; set; }

    public Guid? DefaultWarehouseId { get; set; }

    public string LanguagePreference { get; set; } = "en";

    public string? DashboardPreference { get; set; }

    public string? SignaturePlaceholder { get; set; }

    public string? Avatar { get; set; }
}
