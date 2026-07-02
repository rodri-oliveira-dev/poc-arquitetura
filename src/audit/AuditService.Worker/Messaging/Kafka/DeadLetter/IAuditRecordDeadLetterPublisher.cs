namespace AuditService.Worker.Messaging.Kafka.DeadLetter;

internal interface IAuditRecordDeadLetterPublisher
{
    Task PublishAsync(AuditRecordDeadLetterMessage message, CancellationToken cancellationToken);
}
