namespace AuditService.Worker.Messaging.Kafka;

internal interface IAuditRecordRequestedProcessor
{
    Task<bool> ProcessAsync(string messageValue, CancellationToken cancellationToken);
}
