namespace BalanceService.Worker.Messaging.Abstractions;

public interface IDeadLetterPublisher
{
    Task PublishAsync(DeadLetterMessage message, CancellationToken cancellationToken);
}
