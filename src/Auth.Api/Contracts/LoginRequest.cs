using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Auth.Api.Contracts;

/// <summary>
/// Request de autenticação para emissão de JWT.
/// </summary>
public sealed class LoginRequest
{
    /// <summary>
    /// Nome de usuário fixo da PoC: <c>poc-usuario</c>.
    /// </summary>
    [Required]
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    /// <summary>
    /// Senha fixa da PoC: <c>Poc#123</c>.
    /// </summary>
    [Required]
    [JsonPropertyName("password")]
    public string? Password { get; init; }

    /// <summary>
    /// Scopes desejados separados por espaço.
    /// - Se nulo/vazio: concede TODOS os scopes suportados.
    /// - Se preenchido: valida contra a lista fixa de scopes válidos.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
}
