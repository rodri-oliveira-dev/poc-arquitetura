namespace LedgerService.Domain.Common;

public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
