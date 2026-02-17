using BalanceService.Domain.Balances;

namespace BalanceService.UnitTests.Fixtures;

public static class BalanceFixture
{
    public static LedgerEntryCreatedEvent Event(
        string? id = null,
        string? type = null,
        string? amount = null,
        DateTimeOffset? occurredAt = null,
        DateTimeOffset? createdAt = null,
        string? merchantId = null,
        string? correlationId = null)
        => new(
            Id: id ?? "evt_1",
            Type: type ?? "CREDIT",
            Amount: amount ?? "10.00",
            CreatedAt: createdAt ?? DateTimeOffset.Parse("2026-02-16T00:01:00Z"),
            MerchantId: merchantId ?? "m1",
            OccurredAt: occurredAt ?? DateTimeOffset.Parse("2026-02-16T00:00:00-03:00"),
            Description: null,
            CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
            ExternalReference: null);
}
