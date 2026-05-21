using System.Text.Json.Serialization;

namespace Auth.Api.Contracts.Responses;

/// <summary>
/// Payload de erro padronizado.
/// </summary>
public sealed class ErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; init; } = default!;

    [JsonPropertyName("message")]
    public string Message { get; init; } = default!;
}
