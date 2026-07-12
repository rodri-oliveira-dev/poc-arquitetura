using BalanceService.Domain.Exceptions;

namespace BalanceService.Domain.Balances;

public sealed record BalanceMovement
{
    public string MerchantId
    {
        get;
    }
    public DateOnly Date
    {
        get;
    }
    public Currency Currency
    {
        get;
    }
    public BalanceMovementType Type
    {
        get;
    }
    public BalanceAmount Amount
    {
        get;
    }
    public DateTimeOffset OccurredAt
    {
        get;
    }

    public BalanceMovement(
        string merchantId,
        DateOnly date,
        Currency currency,
        BalanceMovementType type,
        BalanceAmount amount,
        DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(merchantId))
            throw new DomainException("MerchantId is required.");

        if (!Enum.IsDefined(type))
            throw new DomainException("Type must be Credit or Debit.");

        MerchantId = merchantId;
        Date = date;
        Currency = currency;
        Type = type;
        Amount = amount;
        OccurredAt = occurredAt;
    }
}
