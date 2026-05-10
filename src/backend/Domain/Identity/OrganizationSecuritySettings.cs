using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class OrganizationSecuritySettings : AuditableEntity, IOrganizationScopedEntity
{
    public Guid OrganizationId { get; set; }

    public int MinimumPasswordLength { get; set; } = 10;

    public bool RequireUppercase { get; set; } = true;

    public bool RequireLowercase { get; set; } = true;

    public bool RequireNumber { get; set; } = true;

    public bool RequireSymbol { get; set; } = true;

    public int SessionTimeoutMinutes { get; set; } = 480;

    public bool ForceTwoFactor { get; set; }

    public int InviteExpiryDays { get; set; } = 7;

    public string? AllowedEmailDomains { get; set; }

    public int LoginAttemptLimit { get; set; } = 5;

    public int AuditRetentionDays { get; set; } = 365;

    public bool EnableEmailOtp { get; set; }
}
