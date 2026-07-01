using System.Text.Json.Serialization;

namespace LedgerService.Api.Contracts.Responses;

public sealed class RequeueDeadLetterResponse
{
    [JsonPropertyName("requeued")]
    public bool Requeued
    {
        get; init;
    }

    [JsonPropertyName("outboxMessageId")]
    public Guid OutboxMessageId
    {
        get; init;
    }
}
