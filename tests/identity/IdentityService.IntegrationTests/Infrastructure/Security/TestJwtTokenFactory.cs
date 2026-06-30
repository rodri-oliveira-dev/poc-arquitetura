using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

using Microsoft.IdentityModel.Tokens;

namespace IdentityService.IntegrationTests.Infrastructure.Security;

public static class TestJwtTokenFactory
{
    public const string KeycloakIssuer = "http://localhost:8081/realms/poc";
    public const string IdentityAudience = "identity-api";

    public static string CreateToken(
        string issuer = KeycloakIssuer,
        string audiences = IdentityAudience,
        string? scopes = "identity.write identity.read",
        DateTimeOffset? now = null,
        int lifetimeMinutes = 10,
        bool signWithUntrustedKey = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audiences);

        var issuedAt = now ?? DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.AddMinutes(lifetimeMinutes);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, "identity-test-user"),
            new("preferred_username", "identity-test-user"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, issuedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
        ];

        if (!string.IsNullOrWhiteSpace(scopes))
        {
            claims.Add(new Claim("scope", scopes));
        }

        foreach (var audience in audiences.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Aud, audience));
        }

        using var rsa = signWithUntrustedKey ? RSA.Create(2048) : TestJwtKeys.CreateRsa();
        RsaSecurityKey key = new(rsa)
        {
            KeyId = TestJwtKeys.Kid,
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };
        SigningCredentials credentials = new(key, SecurityAlgorithms.RsaSha256);

        JwtSecurityToken token = new(
            issuer: issuer,
            audience: null,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
