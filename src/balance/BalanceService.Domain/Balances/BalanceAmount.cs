using System.Globalization;

using BalanceService.Domain.Exceptions;

namespace BalanceService.Domain.Balances;

public readonly record struct BalanceAmount
{
    public decimal Value
    {
        get;
    }

    public decimal Magnitude => Math.Abs(Value);

    public BalanceAmount(decimal value)
    {
        if (value == 0m)
            throw new DomainException("Amount cannot be 0.");

        Value = value;
    }

    public static BalanceAmount ParseInvariant(string amount)
    {
        return !decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedAmount)
            ? throw new DomainException("Invalid amount format.")
            : new BalanceAmount(parsedAmount);
    }
}
