using PaymentService.Domain.Exceptions;

namespace PaymentService.Domain.Payments;

public readonly record struct MerchantId
{
    public const int MaxLength = 100;

    public string Value
    {
        get;
    }

    public MerchantId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("MerchantId e obrigatorio.");

        var normalized = value.Trim();
        if (normalized.Length > MaxLength)
            throw new DomainException($"MerchantId deve ter no maximo {MaxLength} caracteres.");

        Value = normalized;
    }

    public override string ToString() => Value;
}
