using System.Text.Json.Serialization;

namespace LedgerService.Api.Contracts;

public sealed class RequeueFailedOutboxMessagesRequest
{
    [JsonPropertyName("outboxMessageId")]
    public Guid? OutboxMessageId { get; init; }

    [JsonPropertyName("eventType")]
    public string? EventType { get; init; }

    [JsonPropertyName("occurredFrom")]
    public DateTime? OccurredFrom { get; init; }

    [JsonPropertyName("occurredUntil")]
    public DateTime? OccurredUntil { get; init; }

    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}
