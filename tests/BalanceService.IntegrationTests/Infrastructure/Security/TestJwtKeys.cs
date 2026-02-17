using System.Security.Cryptography;

namespace BalanceService.IntegrationTests.Infrastructure.Security;

public static class TestJwtKeys
{
    public static readonly RSA Rsa = RSA.Create(2048);
    public const string Kid = "test-kid";
}
