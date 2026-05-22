using System.Text.Json.Serialization;

namespace LedgerService.Api.Contracts.Responses;

public sealed class DeadLetterOutboxMessageResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("aggregateType")]
    public string AggregateType { get; init; } = string.Empty;

    [JsonPropertyName("aggregateId")]
    public Guid AggregateId { get; init; }

    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = string.Empty;

    [JsonPropertyName("occurredAt")]
    public DateTime OccurredAt { get; init; }

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; init; }

    [JsonPropertyName("lastError")]
    public string? LastError { get; init; }

    [JsonPropertyName("correlationId")]
    public Guid? CorrelationId { get; init; }

    [JsonPropertyName("traceParent")]
    public string? TraceParent { get; init; }
}
