using BalanceService.Domain.Balances;
using BalanceService.Domain.Exceptions;

namespace BalanceService.Application.IntegrationEvents;

public static class LedgerEntryCreatedIntegrationEventMapper
{
    public static BalanceMovement ToBalanceMovement(LedgerEntryCreatedIntegrationEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var currency = evt.Currency ?? throw new DomainException("LedgerEntryCreated event currency is required.");
        var date = DateOnly.FromDateTime(evt.OccurredAt.Date);
        var occurredAtUtc = evt.OccurredAt.ToUniversalTime();

        return new BalanceMovement(
            evt.MerchantId,
            date,
            new Currency(currency),
            ToMovementType(evt.Type),
            BalanceAmount.ParseInvariant(evt.Amount),
            occurredAtUtc);
    }

    private static BalanceMovementType ToMovementType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new DomainException("Type must be CREDIT or DEBIT.");

        return type.Trim().ToUpperInvariant() switch
        {
            "CREDIT" => BalanceMovementType.Credit,
            "DEBIT" => BalanceMovementType.Debit,
            _ => throw new DomainException("Type must be CREDIT or DEBIT.")
        };
    }
}
