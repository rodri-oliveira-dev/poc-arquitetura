using IdentityService.Domain.Exceptions;

namespace IdentityService.Domain.Users;

public readonly record struct Username
{
    public string Value
    {
        get;
    }

    public Username(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Username is required.");

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
