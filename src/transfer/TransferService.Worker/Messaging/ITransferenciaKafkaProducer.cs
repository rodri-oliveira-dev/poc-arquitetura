using TransferService.Infrastructure.Persistence.Outbox;

namespace TransferService.Worker.Messaging;

public interface ITransferenciaKafkaProducer
{
    Task PublishAsync(TransferenciaOutboxMessage message, string topic, CancellationToken cancellationToken);

    Task PublishDlqAsync(TransferenciaOutboxMessage message, string reason, string dlqTopic, CancellationToken cancellationToken);
}
