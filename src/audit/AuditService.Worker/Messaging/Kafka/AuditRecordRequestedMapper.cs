using AuditService.Application.FunctionalAuditing.CreateAuditRecord;
using AuditService.Worker.Messaging.Kafka.Contracts;

namespace AuditService.Worker.Messaging.Kafka;

internal static class AuditRecordRequestedMapper
{
    public static CreateAuditRecordCommand Map(AuditRecordRequestedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new CreateAuditRecordCommand(
            message.OperationId,
            message.CorrelationId,
            IdempotencyKey: null,
            message.SourceService,
            message.OperationType,
            message.EntityType,
            message.EntityId,
            message.MerchantId,
            message.Actor is null
                ? null
                : new CreateAuditRecordActor(
                    message.Actor.Type,
                    message.Actor.Subject,
                    message.Actor.ClientId),
            message.Status,
            message.Reason,
            message.Metadata,
            message.OccurredAt,
            SourceEventId: message.EventId);
    }
}
