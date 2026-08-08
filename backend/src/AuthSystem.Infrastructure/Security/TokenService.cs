using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AuthSystem.Application.Interfaces;
using AuthSystem.Application.Options;
using AuthSystem.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthSystem.Infrastructure.Security;

/// <summary>
/// The ECDsa signing key is loaded once at startup (see Program.cs) and shared as a singleton
/// with the JWT Bearer validation options, so both use the exact same key material.
/// </summary>
public class TokenService(ECDsa signingKey, IOptions<JwtOptions> options) : ITokenService
{
    private readonly ECDsa _ecdsa = signingKey;
    private readonly JwtOptions _options = options.Value;

    public AccessTokenResult GenerateAccessToken(ApplicationUser user, IReadOnlyList<string> roles)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(new ECDsaSecurityKey(_ecdsa), SecurityAlgorithms.EcdsaSha512);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessTokenResult(jwt, expires);
    }

    public RawRefreshToken GenerateRefreshToken()
    {
        var plainText = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays);
        return new RawRefreshToken(plainText, HashToken(plainText), expiresAt);
    }

    public string HashToken(string rawToken)
    {
        var bytes = SHA512.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
