namespace AuditService.Application.FunctionalAuditing.Ingestion;

public sealed record AuditRecordEnvelope(
    string ContractName,
    int ContractVersion,
    string IdempotencyKey,
    AuditRecordPayload Payload,
    AuditMetadata? Metadata);
