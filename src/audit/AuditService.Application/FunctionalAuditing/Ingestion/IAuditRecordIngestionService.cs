using AuditService.Application.FunctionalAuditing.CreateAuditRecord;

namespace AuditService.Application.FunctionalAuditing.Ingestion;

public interface IAuditRecordIngestionService
{
    Task<CreateAuditRecordResult> IngestAsync(
        AuditRecordEnvelope envelope,
        CancellationToken cancellationToken = default);
}
