using AuditService.Application.FunctionalAuditing.CreateAuditRecord;

namespace AuditService.Application.FunctionalAuditing.Ingestion;

public sealed class AuditRecordMapper : IAuditRecordMapper
{
    public CreateAuditRecordCommand Map(AuditRecordEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(envelope.Payload);

        AuditRecordPayload payload = envelope.Payload;

        return new CreateAuditRecordCommand(
            payload.OperationId,
            envelope.Metadata?.CorrelationId,
            envelope.IdempotencyKey,
            payload.SourceService,
            payload.OperationType,
            payload.EntityType,
            payload.EntityId,
            payload.MerchantId,
            payload.Actor is null
                ? null
                : new CreateAuditRecordActor(
                    payload.Actor.Type,
                    payload.Actor.Subject,
                    payload.Actor.ClientId),
            payload.Status,
            payload.Reason,
            payload.Metadata,
            payload.OccurredAt);
    }
}
