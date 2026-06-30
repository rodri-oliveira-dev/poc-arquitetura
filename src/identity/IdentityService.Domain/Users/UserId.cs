using IdentityService.Domain.Exceptions;

namespace IdentityService.Domain.Users;

public readonly record struct UserId
{
    public Guid Value
    {
        get;
    }

    public UserId(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("UserId is required.");

        Value = value;
    }

    public static UserId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
