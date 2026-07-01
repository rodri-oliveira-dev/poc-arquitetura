namespace AuditService.Worker.Messaging.Kafka;

internal interface IAuditRecordRequestedProcessor
{
    Task<AuditRecordRequestedProcessingResult> ProcessAsync(
        AuditKafkaReceivedMessage message,
        CancellationToken cancellationToken);
}
