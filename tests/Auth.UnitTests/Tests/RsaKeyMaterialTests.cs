using Auth.Api.Security;
using FluentAssertions;
using System.Security.Cryptography;

namespace Auth.UnitTests.Tests;

public sealed class RsaKeyMaterialTests
{
    [Fact]
    public void FromParameters_should_throw_when_private_parameters_missing()
    {
        using var rsa = RSA.Create(2048);
        var publicOnly = rsa.ExportParameters(includePrivateParameters: false);

        var act = () => RsaKeyMaterial.FromParameters(publicOnly);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*incompletos*");
    }

    [Fact]
    public void FromParameters_then_ToParameters_should_roundtrip_rsa_parameters()
    {
        using var rsa = RSA.Create(2048);
        var p = rsa.ExportParameters(includePrivateParameters: true);

        var material = RsaKeyMaterial.FromParameters(p);
        var back = material.ToParameters();

        back.Modulus.Should().BeEquivalentTo(p.Modulus);
        back.Exponent.Should().BeEquivalentTo(p.Exponent);
        back.D.Should().BeEquivalentTo(p.D);
        back.P.Should().BeEquivalentTo(p.P);
        back.Q.Should().BeEquivalentTo(p.Q);
    }

    [Fact]
    public void ToParameters_should_throw_for_invalid_base64url()
    {
        var material = new RsaKeyMaterial
        {
            // valor inválido que vai falhar em Convert.FromBase64String
            D = "@@@",
            DP = "@@@",
            DQ = "@@@",
            Exponent = "@@@",
            InverseQ = "@@@",
            Modulus = "@@@",
            P = "@@@",
            Q = "@@@"
        };

        var act = () => material.ToParameters();

        act.Should().Throw<FormatException>();
    }
}
