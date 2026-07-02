namespace AuditService.Worker.Messaging.Kafka;

internal interface IAuditKafkaConsumerFactory
{
    IAuditKafkaConsumer Create();
}
