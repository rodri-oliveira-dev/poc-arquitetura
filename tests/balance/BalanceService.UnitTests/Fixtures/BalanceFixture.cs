using System.Globalization;

using BalanceService.Application.IntegrationEvents;
using BalanceService.Domain.Balances;

namespace BalanceService.UnitTests.Fixtures;

public static class BalanceFixture
{
    public static LedgerEntryCreatedIntegrationEvent Event(
        string? id = null,
        string? type = null,
        string? amount = null,
        DateTimeOffset? occurredAt = null,
        DateTimeOffset? createdAt = null,
        string? merchantId = null,
        string? correlationId = null,
        string? currency = null)
        => new(
            Id: id ?? "evt_1",
            Type: type ?? "CREDIT",
            Amount: amount ?? "10.00",
            Currency: currency ?? "BRL",
            CreatedAt: createdAt ?? DateTimeOffset.Parse("2026-02-16T00:01:00Z", CultureInfo.InvariantCulture),
            MerchantId: merchantId ?? "m1",
            OccurredAt: occurredAt ?? DateTimeOffset.Parse("2026-02-16T00:00:00-03:00", CultureInfo.InvariantCulture),
            Description: null,
            CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
            ExternalReference: null);

    public static BalanceMovement Movement(
        string? merchantId = null,
        DateOnly? date = null,
        string? currency = null,
        BalanceMovementType type = BalanceMovementType.Credit,
        decimal amount = 10.00m,
        DateTimeOffset? occurredAt = null)
        => new(
            merchantId ?? "m1",
            date ?? new DateOnly(2026, 2, 16),
            new Currency(currency ?? "BRL"),
            type,
            new BalanceAmount(amount),
            occurredAt ?? DateTimeOffset.Parse("2026-02-16T03:00:00Z", CultureInfo.InvariantCulture));
}
