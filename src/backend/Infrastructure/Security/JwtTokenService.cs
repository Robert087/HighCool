using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ERP.Application.Security;
using ERP.Domain.Identity;
using Microsoft.IdentityModel.Tokens;

namespace ERP.Infrastructure.Security;

public sealed class JwtTokenService(JwtSigningOptions signingOptions)
{
    private readonly JwtSigningOptions _signingOptions = signingOptions;

    public (string AccessToken, DateTime ExpiresAt) CreateAccessToken(
        UserAccount user,
        Organization organization,
        OrganizationMembership membership,
        UserSession session)
    {
        var issuer = _signingOptions.Issuer;
        var audience = _signingOptions.Audience;
        var secret = _signingOptions.Secret;
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
