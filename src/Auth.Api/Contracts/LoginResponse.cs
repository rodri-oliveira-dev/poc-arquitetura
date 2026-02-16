using System.Text.Json.Serialization;

namespace Auth.Api.Contracts;

/// <summary>
/// Resposta de emissão de JWT conforme padrão (OAuth-ish) desta PoC.
/// </summary>
public sealed class LoginResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = default!;

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("scope")]
    public string Scope { get; init; } = default!;
}
