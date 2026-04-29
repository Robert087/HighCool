namespace ERP.Application.Security;

public interface IRequestExecutionContext
{
    Guid? UserId { get; }

    Guid? OrganizationId { get; }

    Guid? MembershipId { get; }

    Guid? SessionId { get; }

    string Actor { get; }

    string? Email { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    bool IsAuthenticated { get; }

    bool IsSystem { get; }
}
