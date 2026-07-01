namespace LedgerService.Worker.Messaging.Abstractions;

public sealed class MessagePublishException : Exception
{
    public MessagePublishException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
