using System.Security.Cryptography;

namespace BalanceService.IntegrationTests.Infrastructure.Security;

public static class TestJwtKeys
{
    public const string Kid = "test-kid";

    private static readonly RSAParameters Parameters = CreateParameters();

    public static RSA CreateRsa()
        => RSA.Create(Parameters);

    private static RSAParameters CreateParameters()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportParameters(includePrivateParameters: true);
    }
}
