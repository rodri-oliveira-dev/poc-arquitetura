using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Auth.Api.Options;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Api.Security;

public interface IJwtIssuer
{
    string IssueAccessToken(string subject, string preferredUsername, string scopes, out DateTimeOffset expiresAtUtc);
}

public sealed class JwtIssuer : IJwtIssuer
{
    private readonly AuthOptions _options;
    private readonly IRsaKeyProvider _keys;

    public JwtIssuer(IOptions<AuthOptions> options, IRsaKeyProvider keys)
    {
        _options = options.Value;
        _keys = keys;
    }

    public string IssueAccessToken(string subject, string preferredUsername, string scopes, out DateTimeOffset expiresAtUtc)
    {
        var now = DateTimeOffset.UtcNow;
        var lifetimeMinutes = _options.TokenLifetimeMinutes <= 0 ? 10 : _options.TokenLifetimeMinutes;
        expiresAtUtc = now.AddMinutes(lifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new("preferred_username", preferredUsername),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // iat MUST be numeric date (seconds) in UTC
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("scope", scopes),
        };

        // Requisito: aud configurável.
        // Mantemos simples como 1 string com audiences separadas por espaço (ex.: "ledger-api balance-api").
        var audiences = (_options.Audiences ?? [])
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (audiences.Length > 0)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Aud, string.Join(' ', audiences)));
        }

        var credentials = new SigningCredentials(new RsaSecurityKey(_keys.GetPrivateKey())
        {
            KeyId = _keys.GetKeyId()
        }, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: null,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
