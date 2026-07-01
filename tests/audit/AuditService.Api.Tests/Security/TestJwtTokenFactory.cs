using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Microsoft.IdentityModel.Tokens;

namespace AuditService.Api.Tests.Security;

internal static class TestJwtTokenFactory
{
    public const string KeycloakIssuer = "http://localhost:8081/realms/poc";
    public const string AuditAudience = "audit-api";

    public static string CreateToken(
        string? scopes = "audit.write audit.admin",
        string? merchantIds = "m1",
        string? subject = "audit-test-user",
        string? clientId = "audit-test-client")
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.AddMinutes(10);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, issuedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Aud, AuditAudience)
        ];

        if (!string.IsNullOrWhiteSpace(subject))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, subject));
            claims.Add(new Claim("preferred_username", subject));
        }

        if (!string.IsNullOrWhiteSpace(clientId))
            claims.Add(new Claim("client_id", clientId));

        if (!string.IsNullOrWhiteSpace(scopes))
            claims.Add(new Claim("scope", scopes));

        if (!string.IsNullOrWhiteSpace(merchantIds))
            claims.Add(new Claim("merchant_id", merchantIds));

        using var rsa = TestJwtKeys.CreateRsa();
        RsaSecurityKey key = new(rsa)
        {
            KeyId = TestJwtKeys.Kid,
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };

        JwtSecurityToken token = new(
            issuer: KeycloakIssuer,
            audience: null,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
