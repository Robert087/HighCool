using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class PasswordResetToken : AuditableEntity
{
    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }
}
