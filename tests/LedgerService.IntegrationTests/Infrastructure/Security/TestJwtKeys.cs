using System.Security.Cryptography;

namespace LedgerService.IntegrationTests.Infrastructure.Security;

/// <summary>
/// Material de chave RSA usado apenas em testes de integração.
/// </summary>
public static class TestJwtKeys
{
    // Static para reuso entre testes (evita custo e flakiness)
    public static readonly RSA Rsa = RSA.Create(2048);
    public const string Kid = "test-kid";
}
