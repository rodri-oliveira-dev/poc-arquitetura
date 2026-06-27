using System.Security.Cryptography;

namespace IdentityService.IntegrationTests.Infrastructure.Security;

public static class TestJwtKeys
{
    public const string Kid = "identity-test-kid";

    private static readonly RSAParameters Parameters = CreateParameters();

    public static RSA CreateRsa()
        => RSA.Create(Parameters);

    private static RSAParameters CreateParameters()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportParameters(includePrivateParameters: true);
    }
}
