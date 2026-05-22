using Auth.Api.Security;
using System.Security.Cryptography;

namespace Auth.UnitTests.Security;

public sealed class RsaKeyMaterialTests
{
    [Fact]
    public void FromParameters_should_throw_when_private_parameters_missing()
    {
        using var rsa = RSA.Create(2048);
        var publicOnly = rsa.ExportParameters(includePrivateParameters: false);

        var act = () => RsaKeyMaterial.FromParameters(publicOnly);
        var ex = Assert.Throws<InvalidOperationException>(act);
        Assert.Matches("^" + System.Text.RegularExpressions.Regex.Escape("*incompletos*").Replace("\\*", ".*") + "$", ex.Message);
    }

    [Fact]
    public void FromParameters_then_ToParameters_should_roundtrip_rsa_parameters()
    {
        using var rsa = RSA.Create(2048);
        var p = rsa.ExportParameters(includePrivateParameters: true);

        var material = RsaKeyMaterial.FromParameters(p);
        var back = material.ToParameters();
        Assert.Equivalent(p.Modulus, back.Modulus);
        Assert.Equivalent(p.Exponent, back.Exponent);
        Assert.Equivalent(p.D, back.D);
        Assert.Equivalent(p.P, back.P);
        Assert.Equivalent(p.Q, back.Q);
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

        void Act() => _ = material.ToParameters();
        Assert.Throws<FormatException>(Act);
    }
}
