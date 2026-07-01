namespace AuditService.Worker.Messaging.Kafka;

internal sealed record AuditKafkaReceivedMessage(
    string Payload,
    string Topic,
    int Partition,
    long Offset);
