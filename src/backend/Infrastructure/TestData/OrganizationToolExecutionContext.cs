using ERP.Application.Security;
using ERP.Application.TestData;

namespace ERP.Infrastructure.TestData;

public sealed class OrganizationToolExecutionContext : IRequestExecutionContext, IOrganizationScopedToolExecutionContext
{
    private Guid? _organizationId;

    public Guid? UserId => null;

    public Guid? OrganizationId => _organizationId;

    public Guid? MembershipId => null;

    public Guid? SessionId => null;

    public string Actor => "highcool-tool";

    public string? Email => null;

    public string? IpAddress => null;

    public string? UserAgent => null;

    public bool IsAuthenticated => true;

    public bool IsSystem => false;

    public void SetOrganization(Guid organizationId)
    {
        _organizationId = organizationId == Guid.Empty
            ? throw new ArgumentException("Organization id is required.", nameof(organizationId))
            : organizationId;
    }
}
