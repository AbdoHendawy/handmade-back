using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Handmade.Application.Abstractions.Security;
using Handmade.Application.Identity;
using Handmade.Application.Identity.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Handmade.Infrastructure.Identity.Security;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;

    public JwtTokenGenerator(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public AccessTokenResult GenerateAccessToken(
        Guid userId,
        string email,
        IEnumerable<string> roles,
        int securityStamp)
    {
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes);

        List<Claim> claims =
        [
            new(AuthClaimTypes.Subject, userId.ToString()),
            new(AuthClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(AuthClaimTypes.SecurityStamp, securityStamp.ToString())
        ];

        foreach (string role in roles.Distinct(StringComparer.Ordinal))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_settings.SecretKey));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        string encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessTokenResult(encoded, expiresAt);
    }
}
