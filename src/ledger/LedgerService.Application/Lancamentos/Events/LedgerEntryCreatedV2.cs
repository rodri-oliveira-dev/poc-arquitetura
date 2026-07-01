using System.Text.Json.Serialization;

namespace LedgerService.Application.Lancamentos.Events;

public sealed record LedgerEntryCreatedV2(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("amount")] string Amount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("createdAt")] string CreatedAt,
    [property: JsonPropertyName("merchantId")] string MerchantId,
    [property: JsonPropertyName("occurredAt")] string OccurredAt,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("externalReference")] string? ExternalReference)
{
    public const string EventType = "LedgerEntryCreated.v2";
}
