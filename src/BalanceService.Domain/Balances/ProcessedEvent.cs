using BalanceService.Domain.Common;
using BalanceService.Domain.Exceptions;

namespace BalanceService.Domain.Balances;

/// <summary>
/// Registro de idempotência para eventos processados pelo consumer.
/// </summary>
public sealed class ProcessedEvent : Entity
{
    public string EventId { get; private set; } = string.Empty;
    public string MerchantId { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }

    // EF Core
    private ProcessedEvent() { }

    public ProcessedEvent(string eventId, string merchantId, DateTimeOffset occurredAt, DateTimeOffset processedAt)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            throw new DomainException("EventId is required.");

        if (string.IsNullOrWhiteSpace(merchantId))
            throw new DomainException("MerchantId is required.");

        EventId = eventId;
        MerchantId = merchantId;
        OccurredAt = occurredAt;
        ProcessedAt = processedAt;
    }
}
