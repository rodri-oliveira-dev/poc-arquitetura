using BalanceService.Domain.Exceptions;

namespace BalanceService.Domain.Balances;

public readonly record struct Currency
{
    public string Code
    {
        get;
    }

    public Currency(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Currency must be a 3-letter code.");

        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length != 3)
            throw new DomainException("Currency must be a 3-letter code.");

        Code = normalized;
    }

    public override string ToString() => Code;
}
