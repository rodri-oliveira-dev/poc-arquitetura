using PaymentService.Domain.Exceptions;

namespace PaymentService.Domain.Payments;

public readonly record struct LedgerEntryReference
{
    public Guid Value
    {
        get;
    }

    public LedgerEntryReference(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("LedgerEntryReference e obrigatoria.");

        Value = value;
    }

    public override string ToString() => Value.ToString();
}
