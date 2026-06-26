using System.Net.Mail;

using IdentityService.Domain.Exceptions;

namespace IdentityService.Domain.Users;

public readonly record struct Email
{
    public string Value
    {
        get;
    }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Email is required.");

        var normalized = value.Trim();

        if (!IsValid(normalized))
            throw new DomainException("Email is invalid.");

        Value = normalized;
    }

    public override string ToString() => Value;

    private static bool IsValid(string value)
    {
        try
        {
            var address = new MailAddress(value);

            return string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
