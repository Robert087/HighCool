using System.Security.Claims;
using ERP.Application.Security;
using Microsoft.AspNetCore.Http;

namespace ERP.Infrastructure.Security;

public sealed class HttpRequestExecutionContext(IHttpContextAccessor httpContextAccessor) : IRequestExecutionContext
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public Guid? UserId => ParseGuid(ClaimTypes.NameIdentifier);

    public Guid? OrganizationId => ParseGuid(CustomClaimTypes.OrganizationId);

    public Guid? MembershipId => ParseGuid(CustomClaimTypes.MembershipId);

    public Guid? SessionId => ParseGuid(CustomClaimTypes.SessionId);

    public string Actor => Email ?? "anonymous";

    public string? Email => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public bool IsSystem => _httpContextAccessor.HttpContext is null;

    private Guid? ParseGuid(string claimType)
    {
        var raw = _httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
        return Guid.TryParse(raw, out var value) ? value : null;
    }
}
