namespace TransferService.Worker.Messaging;

public sealed class TransferenciaKafkaPublishException : Exception
{
    public TransferenciaKafkaPublishException(string message, bool isTransient, Exception? innerException = null)
        : base(message, innerException)
    {
        IsTransient = isTransient;
    }

    public bool IsTransient { get; }
}
