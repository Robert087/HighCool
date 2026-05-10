using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class UserSession : AuditableEntity
{
    public Guid UserId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid MembershipId { get; set; }

    public string SessionTokenHash { get; set; } = string.Empty;

    public string? DeviceName { get; set; }

    public string? Browser { get; set; }

    public string? IpAddress { get; set; }

    public bool RememberMe { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? RevokedBy { get; set; }

    public bool IsActive { get; set; } = true;
}
