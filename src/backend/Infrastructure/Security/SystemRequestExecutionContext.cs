using ERP.Application.Security;

namespace ERP.Infrastructure.Security;

public sealed class SystemRequestExecutionContext : IRequestExecutionContext
{
    public static readonly SystemRequestExecutionContext Instance = new();

    public Guid? UserId => null;

    public Guid? OrganizationId => null;

    public Guid? MembershipId => null;

    public Guid? SessionId => null;

    public string Actor => "system";

    public string? Email => null;

    public string? IpAddress => null;

    public string? UserAgent => null;

    public bool IsAuthenticated => false;

    public bool IsSystem => true;
}
