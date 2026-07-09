using System.Globalization;

using PaymentService.Domain.Exceptions;

namespace PaymentService.Domain.Payments;

public readonly record struct Money
{
    public decimal Amount
    {
        get;
    }

    public Currency Currency
    {
        get;
    }

    public Money(decimal amount, Currency currency)
    {
        if (amount <= 0m)
            throw new DomainException("Amount deve ser maior que zero.");

        Amount = amount;
        Currency = currency;
    }

    public override string ToString() => $"{Amount.ToString("0.00", CultureInfo.InvariantCulture)} {Currency.Code}";
}
