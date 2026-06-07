using System.Text.Json.Serialization;

namespace BalanceService.Domain.Balances;

/// <summary>
/// Evento de criacao de lancamento recebido pelo Balance.
/// </summary>
/// <remarks>
/// Contrato atual (evidência: payload descrito na tarefa). Campos seguem JSON camelCase.
/// </remarks>
public sealed record LedgerEntryCreatedEvent(
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
