using TransferService.Domain.Exceptions;

namespace TransferService.Domain.Sagas;

public readonly record struct MerchantId
{
    public string Value { get; }

    public MerchantId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("MerchantId e obrigatorio.");

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
