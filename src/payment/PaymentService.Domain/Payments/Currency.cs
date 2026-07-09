using PaymentService.Domain.Exceptions;

namespace PaymentService.Domain.Payments;

public readonly record struct Currency
{
    public const string BrlCode = "BRL";
    public const int CodeLength = 3;

    public string Code
    {
        get;
    }

    public Currency(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Currency e obrigatoria.");

        var normalized = code.Trim().ToUpperInvariant();
        if (!string.Equals(normalized, BrlCode, StringComparison.Ordinal))
            throw new DomainException("Somente BRL e suportada no MVP de pagamentos.");

        Code = normalized;
    }

    public static Currency Brl => new(BrlCode);

    public override string ToString() => Code;
}
