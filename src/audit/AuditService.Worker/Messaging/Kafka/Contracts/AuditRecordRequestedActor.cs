namespace AuditService.Worker.Messaging.Kafka.Contracts;

internal sealed record AuditRecordRequestedActor(
    string Type,
    string? Subject,
    string? ClientId);
