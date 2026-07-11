using BalanceService.Domain.Common;
using BalanceService.Domain.Exceptions;

namespace BalanceService.Domain.Balances;

/// <summary>
/// Consolidado diario por Merchant + Data + Moeda.
/// </summary>
public sealed class DailyBalance : Entity, IAggregateRoot
{
    public string MerchantId { get; private set; } = string.Empty;
    public DateOnly Date
    {
        get; private set;
    }
    public string Currency { get; private set; } = string.Empty;

    public decimal TotalCredits
    {
        get; private set;
    }
    public decimal TotalDebits
    {
        get; private set;
    }
    public decimal NetBalance
    {
        get; private set;
    }

    public DateTimeOffset AsOf
    {
        get; private set;
    }
    public DateTimeOffset UpdatedAt
    {
        get; private set;
    }

    // EF Core
    private DailyBalance()
    {
    }

    public DailyBalance(string merchantId, DateOnly date, string currency, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(merchantId))
            throw new DomainException("MerchantId is required.");

        var normalizedCurrency = new Currency(currency);

        MerchantId = merchantId;
        Date = date;
        Currency = normalizedCurrency.Code;

        TotalCredits = 0m;
        TotalDebits = 0m;
        NetBalance = 0m;
        AsOf = DateTimeOffset.MinValue;
        UpdatedAt = now;
    }

    public void Apply(BalanceMovement movement, DateTimeOffset now)
    {
        if (movement is null)
            throw new DomainException("Movement is required.");

        switch (movement.Type)
        {
            case BalanceMovementType.Credit:
                TotalCredits += movement.Amount.Magnitude;
                break;
            case BalanceMovementType.Debit:
                TotalDebits += movement.Amount.Magnitude;
                break;
            default:
                throw new DomainException("Type must be Credit or Debit.");
        }

        NetBalance = TotalCredits - TotalDebits;

        if (movement.OccurredAt > AsOf)
            AsOf = movement.OccurredAt;

        UpdatedAt = now;
    }
}
