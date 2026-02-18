using Auth.Api.Options;
using Auth.Api.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace Auth.UnitTests.Tests;

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

        token.Issuer.Should().Be("https://auth-api");
        token.Header.Alg.Should().Be("RS256");
        token.Header.Kid.Should().Be("kid-123");

        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == "poc-usuario");
        token.Claims.Should().Contain(c => c.Type == "preferred_username" && c.Value == "poc-usuario");
        token.Claims.Should().Contain(c => c.Type == "scope" && c.Value == "ledger.write balance.read");

        // aud é emitido como string única com audiences separadas por espaço
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Aud && c.Value == "ledger-api balance-api");

        expiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(9));
        expiresAt.Should().BeBefore(DateTimeOffset.UtcNow.AddMinutes(11));

        token.ValidTo.Should().BeCloseTo(expiresAt.UtcDateTime, precision: TimeSpan.FromSeconds(2));

        keys.VerifyAll();
    }
}
