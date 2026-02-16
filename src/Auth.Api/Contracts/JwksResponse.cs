using System.Text.Json.Serialization;

namespace Auth.Api.Contracts;

public sealed class JwksResponse
{
    [JsonPropertyName("keys")]
    public List<JwkKey> Keys { get; init; } = [];
}

public sealed class JwkKey
{
    [JsonPropertyName("kty")]
    public string Kty { get; init; } = "RSA";

    [JsonPropertyName("use")]
    public string Use { get; init; } = "sig";

    [JsonPropertyName("alg")]
    public string Alg { get; init; } = "RS256";

    [JsonPropertyName("kid")]
    public string Kid { get; init; } = default!;

    /// <summary>
    /// Modulus (base64url).
    /// </summary>
    [JsonPropertyName("n")]
    public string N { get; init; } = default!;

    /// <summary>
    /// Exponent (base64url).
    /// </summary>
    [JsonPropertyName("e")]
    public string E { get; init; } = default!;
}
