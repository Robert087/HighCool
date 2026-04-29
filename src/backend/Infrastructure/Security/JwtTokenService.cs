using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ERP.Application.Security;
using ERP.Domain.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ERP.Infrastructure.Security;

public sealed class JwtTokenService(IConfiguration configuration)
{
    private readonly IConfiguration _configuration = configuration;

    public (string AccessToken, DateTime ExpiresAt) CreateAccessToken(
        UserAccount user,
        Organization organization,
        OrganizationMembership membership,
        UserSession session)
    {
        var issuer = _configuration["Authentication:Issuer"] ?? "HighCool";
        var audience = _configuration["Authentication:Audience"] ?? "HighCool.Client";
        var secret = _configuration["Authentication:JwtSecret"] ?? "highcool-dev-secret-change-me-immediately";
        var expiresAt = session.ExpiresAt;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(CustomClaimTypes.OrganizationId, organization.Id.ToString()),
            new(CustomClaimTypes.MembershipId, membership.Id.ToString()),
            new(CustomClaimTypes.SessionId, session.Id.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
