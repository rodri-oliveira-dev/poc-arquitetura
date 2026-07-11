using System.Text.Json.Serialization;

namespace BalanceService.Application.IntegrationEvents;

/// <summary>
/// Evento de integracao recebido do LedgerService.
/// </summary>
public sealed record LedgerEntryCreatedIntegrationEvent(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("amount")] string Amount,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("merchantId")] string MerchantId,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("externalReference")] string? ExternalReference
);
