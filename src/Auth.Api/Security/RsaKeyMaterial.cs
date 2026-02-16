using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Auth.Api.Security;

/// <summary>
/// Material de chave RSA persistido em disco.
/// 
/// Observação: armazenamos os parâmetros privados para conseguir assinar tokens após reinício.
/// Não há criptografia neste arquivo (POC). Em produção, usar secret manager/KMS.
/// </summary>
public sealed class RsaKeyMaterial
{
    [JsonPropertyName("d")]
    public string D { get; init; } = default!;

    [JsonPropertyName("dp")]
    public string DP { get; init; } = default!;

    [JsonPropertyName("dq")]
    public string DQ { get; init; } = default!;

    [JsonPropertyName("e")]
    public string Exponent { get; init; } = default!;

    [JsonPropertyName("inverseQ")]
    public string InverseQ { get; init; } = default!;

    [JsonPropertyName("n")]
    public string Modulus { get; init; } = default!;

    [JsonPropertyName("p")]
    public string P { get; init; } = default!;

    [JsonPropertyName("q")]
    public string Q { get; init; } = default!;

    public static RsaKeyMaterial FromParameters(RSAParameters p)
    {
        if (p.D is null || p.DP is null || p.DQ is null || p.InverseQ is null ||
            p.Modulus is null || p.Exponent is null || p.P is null || p.Q is null)
        {
            throw new InvalidOperationException("RSAParameters incompletos para persistência (chave privada ausente).");
        }

        return new RsaKeyMaterial
        {
            D = Base64Url(p.D),
            DP = Base64Url(p.DP),
            DQ = Base64Url(p.DQ),
            InverseQ = Base64Url(p.InverseQ),
            Modulus = Base64Url(p.Modulus),
            Exponent = Base64Url(p.Exponent),
            P = Base64Url(p.P),
            Q = Base64Url(p.Q)
        };
    }

    public RSAParameters ToParameters()
        => new()
        {
            D = Base64UrlDecode(D),
            DP = Base64UrlDecode(DP),
            DQ = Base64UrlDecode(DQ),
            Exponent = Base64UrlDecode(Exponent),
            InverseQ = Base64UrlDecode(InverseQ),
            Modulus = Base64UrlDecode(Modulus),
            P = Base64UrlDecode(P),
            Q = Base64UrlDecode(Q)
        };

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value
            .Replace('-', '+')
            .Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
