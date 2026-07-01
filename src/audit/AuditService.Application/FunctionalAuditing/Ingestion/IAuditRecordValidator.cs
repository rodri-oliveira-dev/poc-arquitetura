namespace AuditService.Application.FunctionalAuditing.Ingestion;

public interface IAuditRecordValidator
{
    void ValidateAndThrow(AuditRecordEnvelope envelope);
}
