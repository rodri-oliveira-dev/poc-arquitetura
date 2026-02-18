using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BalanceService.IntegrationTests.Infrastructure.Security;

public static class TestJwtTokenFactory
{
    public static string CreateToken(
        string issuer,
        string audiences,
        string scopes,
        DateTimeOffset? now = null,
        int lifetimeMinutes = 10)
    {
        var n = now ?? DateTimeOffset.UtcNow;
        var exp = n.AddMinutes(lifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "poc-usuario"),
            new("preferred_username", "poc-usuario"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, n.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("scope", scopes),
            new(JwtRegisteredClaimNames.Aud, audiences),
        };

        var credentials = new SigningCredentials(new RsaSecurityKey(TestJwtKeys.Rsa)
        {
            KeyId = TestJwtKeys.Kid
        }, SecurityAlgorithms.RsaSha256);

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
