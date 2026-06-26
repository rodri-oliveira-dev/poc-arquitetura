using IdentityService.Domain.Exceptions;

namespace IdentityService.Domain.Users;

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
            throw new DomainException("MerchantId is required.");

        var normalized = value.Trim();

        if (normalized.Length > MaxLength)
            throw new DomainException("MerchantId must be at most 100 characters.");

        Value = normalized;
    }

    public override string ToString() => Value;
}
