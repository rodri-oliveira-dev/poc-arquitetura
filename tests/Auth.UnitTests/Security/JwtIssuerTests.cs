using Auth.Api.Options;
using Auth.Api.Security;

using Microsoft.Extensions.Options;

using Moq;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace Auth.UnitTests.Security;

public sealed class JwtIssuerTests
{
    [Fact]
    public void IssueAccessToken_should_include_expected_claims_and_exp_about_10min_and_kid_header()
    {
        using var rsa = RSA.Create(2048);

        var options = Options.Create(new AuthOptions
        {
            Issuer = "https://auth-api",
            Audiences = ["ledger-api", "balance-api"],
            AuthorizedMerchants = ["m1", "tese"],
            TokenLifetimeMinutes = 10,
            KeyPath = ".\\TestKeys\\ignored.json"
        });

        var keys = new Mock<IRsaKeyProvider>(MockBehavior.Strict);
        keys.Setup(x => x.GetPrivateKey()).Returns(rsa);
        keys.Setup(x => x.GetKeyId()).Returns("kid-123");

        var sut = new JwtIssuer(options, keys.Object);

        var jwt = sut.IssueAccessToken(
            subject: "poc-usuario",
            preferredUsername: "poc-usuario",
            scopes: "ledger.write balance.read",
            out var expiresAt);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        Assert.Equal("https://auth-api", token.Issuer);
        Assert.Equal("RS256", token.Header.Alg);
        Assert.Equal("kid-123", token.Header.Kid);
        Assert.Contains(token.Claims, c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == "poc-usuario");
        Assert.Contains(token.Claims, c => c.Type == "preferred_username" && c.Value == "poc-usuario");
        Assert.Contains(token.Claims, c => c.Type == "scope" && c.Value == "ledger.write balance.read");
        Assert.Contains(token.Claims, c => c.Type == "merchant_id" && c.Value == "m1 tese");
        // aud é emitido como string única com audiences separadas por espaço
        Assert.Contains(token.Claims, c => c.Type == JwtRegisteredClaimNames.Aud && c.Value == "ledger-api balance-api");
        Assert.True(expiresAt > DateTimeOffset.UtcNow.AddMinutes(9));
        Assert.True(expiresAt < DateTimeOffset.UtcNow.AddMinutes(11));
        Assert.InRange(token.ValidTo, expiresAt.UtcDateTime - TimeSpan.FromSeconds(2), expiresAt.UtcDateTime + TimeSpan.FromSeconds(2));
        keys.VerifyAll();
    }
}
