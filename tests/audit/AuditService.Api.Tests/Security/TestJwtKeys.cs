using System.Security.Cryptography;

namespace AuditService.Api.Tests.Security;

internal static class TestJwtKeys
{
    public const string Kid = "audit-test-key";

    private static readonly RSAParameters PrivateKey;

    static TestJwtKeys()
    {
        using RSA rsa = RSA.Create(2048);
        PrivateKey = rsa.ExportParameters(includePrivateParameters: true);
    }

    public static RSA CreateRsa()
    {
        RSA rsa = RSA.Create();
        rsa.ImportParameters(PrivateKey);
        return rsa;
    }
}
