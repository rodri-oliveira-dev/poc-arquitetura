using System.Text.Json.Serialization;

namespace LedgerService.Api.Contracts;

public sealed class RequeueFailedOutboxMessagesResponse
{
    [JsonPropertyName("requeuedCount")]
    public int RequeuedCount { get; init; }

    [JsonPropertyName("outboxMessageIds")]
    public IReadOnlyList<Guid> OutboxMessageIds { get; init; } = Array.Empty<Guid>();
}
