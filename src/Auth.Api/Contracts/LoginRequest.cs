using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Auth.Api.Contracts;

/// <summary>
/// Request de autenticação para emissão de JWT.
/// </summary>
public sealed class LoginRequest
{
    /// <summary>
    /// Nome de usuario local da PoC, configurado via <c>Auth:DevelopmentUser:Username</c>.
    /// </summary>
    [Required]
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    /// <summary>
    /// Senha local da PoC, configurada via <c>Auth:DevelopmentUser:Password</c>.
    /// </summary>
    [Required]
    [JsonPropertyName("password")]
    public string? Password { get; init; }

    /// <summary>
    /// Scopes desejados separados por espaço.
    /// - Se nulo/vazio: rejeita a solicitacao.
    /// - Se preenchido: valida contra a lista fixa de scopes válidos.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
}
