using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Microsoft.IdentityModel.Tokens;

namespace TransferService.IntegrationTests.Infrastructure.Security;

public static class TestJwtTokenFactory
{
    public const string KeycloakIssuer = "http://localhost:8081/realms/poc";
    public const string TransferAudience = "transfer-api";
    public const string KeycloakScopes = "transfer.write transfer.read";

    public static string CreateToken(
        string issuer = KeycloakIssuer,
        string audiences = TransferAudience,
        string? scopes = KeycloakScopes,
        string? merchantIds = "m1",
        DateTimeOffset? now = null,
        int lifetimeMinutes = 10,
        bool signWithUntrustedKey = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audiences);

        var n = now ?? DateTimeOffset.UtcNow;
        var exp = n.AddMinutes(lifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "poc-usuario"),
            new("preferred_username", "poc-usuario"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, n.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        };

        if (!string.IsNullOrWhiteSpace(scopes))
            claims.Add(new Claim("scope", scopes));

        foreach (var audience in audiences.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            claims.Add(new Claim(JwtRegisteredClaimNames.Aud, audience));

        if (!string.IsNullOrWhiteSpace(merchantIds))
            claims.Add(new Claim("merchant_id", merchantIds));

        using var rsa = signWithUntrustedKey ? System.Security.Cryptography.RSA.Create(2048) : TestJwtKeys.CreateRsa();
        var key = new RsaSecurityKey(rsa)
        {
            KeyId = TestJwtKeys.Kid,
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: null,
            claims: claims,
            notBefore: n.UtcDateTime,
            expires: exp.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
