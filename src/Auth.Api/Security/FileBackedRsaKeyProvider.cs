using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Auth.Api.Options;

using Microsoft.Extensions.Options;

namespace Auth.Api.Security;

/// <summary>
/// Provider de chave RSA com persistência em arquivo local.
///
/// Regras:
/// - Se o arquivo existir: carrega a chave.
/// - Se não existir: gera e salva.
/// - O kid é estável e derivado do fingerprint SHA-256 do (modulus||exponent) da chave pública.
/// </summary>
public sealed class FileBackedRsaKeyProvider : IRsaKeyProvider
{
    private readonly ILogger<FileBackedRsaKeyProvider> _logger;
    private readonly AuthOptions _options;

    private readonly Lazy<State> _state;

    public FileBackedRsaKeyProvider(IOptions<AuthOptions> options, ILogger<FileBackedRsaKeyProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
        _state = new Lazy<State>(Initialize, isThreadSafe: true);
    }

    public RSA GetPrivateKey() => _state.Value.Private;
    public RSA GetPublicKey() => _state.Value.Public;
    public string GetKeyId() => _state.Value.Kid;

    /// <summary>
    /// Este provider é singleton e mantém a chave em memória durante a vida do processo.
    /// Implementamos Dispose para liberar as instâncias de <see cref="RSA"/> em shutdown.
    /// </summary>
    public void Dispose()
    {
        if (_state.IsValueCreated)
        {
            _state.Value.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private State Initialize()
    {
        var keyPath = _options.KeyPath;
        EnsureDirectoryExistsForFile(keyPath);

        if (File.Exists(keyPath))
        {
            try
            {
                var json = File.ReadAllText(keyPath, Encoding.UTF8);
                var material = JsonSerializer.Deserialize<RsaKeyMaterial>(json);
                if (material is null)
                    throw new InvalidOperationException("Arquivo de chave RSA inválido (json null).");

                // CA2000: as instâncias são mantidas vivas pelo provider (singleton) e
                // liberadas em Dispose().
#pragma warning disable CA2000
                var privateKey = RSA.Create();
                privateKey.ImportParameters(material.ToParameters());

                var publicKey = RSA.Create();
                publicKey.ImportParameters(privateKey.ExportParameters(includePrivateParameters: false));
#pragma warning restore CA2000

                var kid = ComputeKid(publicKey);

                _logger.LogInformation("Chave RSA carregada de {KeyPath}. kid={Kid}", keyPath, kid);

                return new State(privateKey, publicKey, kid);
            }
            catch (Exception ex)
            {
                // NÃO tentamos gerar uma chave nova silenciosamente para não quebrar tokens antigos.
                // A falha deve ser explícita.
                _logger.LogError(ex, "Falha ao carregar chave RSA de {KeyPath}.", keyPath);
                throw;
            }
        }

        // Gera + salva
        // CA2000: a instância temporária gerada aqui é descartada após exportarmos os parâmetros.
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(includePrivateParameters: true);
        var toPersist = RsaKeyMaterial.FromParameters(parameters);

        var persistJson = JsonSerializer.Serialize(toPersist, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(keyPath, persistJson, Encoding.UTF8);

        // CA2000: as instâncias são mantidas vivas pelo provider (singleton) e
        // liberadas em Dispose().
#pragma warning disable CA2000
        var privateRsa = RSA.Create();
        privateRsa.ImportParameters(parameters);

        var publicRsa = RSA.Create();
        publicRsa.ImportParameters(rsa.ExportParameters(includePrivateParameters: false));
#pragma warning restore CA2000

        var newKid = ComputeKid(publicRsa);
        _logger.LogInformation("Nova chave RSA gerada e salva em {KeyPath}. kid={Kid}", keyPath, newKid);

        return new State(privateRsa, publicRsa, newKid);
    }

    private static void EnsureDirectoryExistsForFile(string filePath)
    {
        var full = Path.GetFullPath(filePath);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }

    private static string ComputeKid(RSA publicKey)
    {
        var p = publicKey.ExportParameters(includePrivateParameters: false);
        if (p.Modulus is null || p.Exponent is null)
            throw new InvalidOperationException("Chave pública RSA sem modulus/exponent.");

        // fingerprint = SHA-256(modulus || exponent)
        var bytes = new byte[p.Modulus.Length + p.Exponent.Length];
        Buffer.BlockCopy(p.Modulus, 0, bytes, 0, p.Modulus.Length);
        Buffer.BlockCopy(p.Exponent, 0, bytes, p.Modulus.Length, p.Exponent.Length);

        var hash = SHA256.HashData(bytes);
        return Base64Url(hash);
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed record State(RSA Private, RSA Public, string Kid) : IDisposable
    {
        public void Dispose()
        {
            Private.Dispose();
            Public.Dispose();
        }
    }
}
