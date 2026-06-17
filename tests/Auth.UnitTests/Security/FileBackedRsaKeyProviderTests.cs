using Auth.Api.Options;
using Auth.Api.Security;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System.Security.Cryptography;

namespace Auth.UnitTests.Security;

public sealed class FileBackedRsaKeyProviderTests
{
    [Fact]
    public void When_file_does_not_exist_should_generate_and_persist_and_return_stable_kid()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"auth-keytests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var keyPath = Path.Combine(tempDir, "rsa.json");

        var options = Options.Create(new AuthOptions
        {
            Issuer = "https://auth-api",
            Audiences = ["ledger-api"],
            TokenLifetimeMinutes = 10,
            KeyPath = keyPath
        });

        var provider1 = new FileBackedRsaKeyProvider(options, NullLogger<FileBackedRsaKeyProvider>.Instance);

        // A inicialização e persistência acontecem de forma lazy (primeiro acesso à chave).
        var kid1 = provider1.GetKeyId();
        Assert.True(File.Exists(keyPath));
        Assert.False(string.IsNullOrWhiteSpace(kid1));
        // A chave pública deve ser consistente com o KID (indiretamente: não deve lançar)
        using var pub1 = provider1.GetPublicKey();
        Assert.NotNull(pub1.ExportParameters(false).Modulus);
        // Segunda instância deve carregar do arquivo e manter KID
        var provider2 = new FileBackedRsaKeyProvider(options, NullLogger<FileBackedRsaKeyProvider>.Instance);
        var kid2 = provider2.GetKeyId();
        Assert.Equal(kid1, kid2);
        provider1.Dispose();
        provider2.Dispose();
    }

    [Fact]
    public void When_file_contains_invalid_json_should_throw_and_not_overwrite()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"auth-keytests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var keyPath = Path.Combine(tempDir, "rsa.json");
        File.WriteAllText(keyPath, "{ this-is-not-valid-json ");

        var options = Options.Create(new AuthOptions
        {
            Issuer = "https://auth-api",
            Audiences = ["ledger-api"],
            TokenLifetimeMinutes = 10,
            KeyPath = keyPath
        });

        var act = () => new FileBackedRsaKeyProvider(options, NullLogger<FileBackedRsaKeyProvider>.Instance).GetKeyId();
        Assert.ThrowsAny<Exception>(act);
        Assert.Contains("this-is-not-valid-json", File.ReadAllText(keyPath));
    }

    [Fact]
    public void Dispose_should_dispose_rsa_instances_when_created()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"auth-keytests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var keyPath = Path.Combine(tempDir, "rsa.json");

        var options = Options.Create(new AuthOptions
        {
            Issuer = "https://auth-api",
            Audiences = ["ledger-api"],
            TokenLifetimeMinutes = 10,
            KeyPath = keyPath
        });

        var provider = new FileBackedRsaKeyProvider(options, NullLogger<FileBackedRsaKeyProvider>.Instance);
        _ = provider.GetKeyId();

        // Não temos como observar diretamente o Dispose do RSA sem expor internals,
        // mas garantimos que o Dispose() não lança.
        provider.Dispose();
    }
}
