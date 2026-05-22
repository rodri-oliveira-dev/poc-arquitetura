using System.Text.Json.Serialization;

namespace LedgerService.Api.Contracts.Requests;

public sealed class RequeueDeadLetterRequest
{
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}
