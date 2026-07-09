using PaymentService.Domain.Exceptions;

namespace PaymentService.Domain.Payments;

public readonly record struct ExternalReference
{
    public const int MaxLength = 200;

    public string Value
    {
        get;
    }

    public ExternalReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("ExternalReference nao pode ser vazia quando informada.");

        var normalized = value.Trim();
        if (normalized.Length > MaxLength)
            throw new DomainException($"ExternalReference deve ter no maximo {MaxLength} caracteres.");

        Value = normalized;
    }

    public override string ToString() => Value;
}
