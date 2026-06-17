using TransferService.Domain.Exceptions;
using System.Globalization;

namespace TransferService.Domain.Sagas;

public readonly record struct TransferAmount
{
    public decimal Value { get; }

    public TransferAmount(decimal value)
    {
        if (value <= 0m)
            throw new DomainException("Amount deve ser maior que zero.");

        Value = value;
    }

    public override string ToString() => Value.ToString("0.00", CultureInfo.InvariantCulture);
}
