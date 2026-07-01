namespace TransferService.Worker.Ledger;

public sealed class LedgerAuthenticationException : Exception
{
    public LedgerAuthenticationException(string message)
        : base(message)
    {
    }

    public LedgerAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
