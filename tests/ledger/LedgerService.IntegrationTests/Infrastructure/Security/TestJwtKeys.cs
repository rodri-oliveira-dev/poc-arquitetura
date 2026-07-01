using System.Security.Cryptography;

namespace LedgerService.IntegrationTests.Infrastructure.Security;

/// <summary>
/// Material de chave RSA usado apenas em testes de integração.
/// </summary>
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
