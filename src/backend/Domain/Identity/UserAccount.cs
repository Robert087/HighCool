using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class UserAccount : AuditableEntity
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool EmailVerified { get; set; }

    public UserAccountStatus Status { get; set; } = UserAccountStatus.Active;

    public DateTime? LastLoginAt { get; set; }

    public string? LastLoginIpAddress { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockedUntil { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<OrganizationMembership> Memberships { get; set; } = new List<OrganizationMembership>();
}
