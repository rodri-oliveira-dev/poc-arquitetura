using PaymentService.Domain.Exceptions;

namespace PaymentService.Domain.Payments;

public readonly record struct PaymentId
{
    public Guid Value
    {
        get;
    }

    public PaymentId(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("PaymentId e obrigatorio.");

        Value = value;
    }

    public static PaymentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
