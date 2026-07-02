namespace AuditService.Worker.Messaging.Kafka.DeadLetter;

internal interface IAuditKafkaDeadLetterProducerFactory
{
    IAuditKafkaDeadLetterProducer Create();
}
