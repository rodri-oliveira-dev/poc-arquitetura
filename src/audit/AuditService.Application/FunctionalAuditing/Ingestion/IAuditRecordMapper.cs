using AuditService.Application.FunctionalAuditing.CreateAuditRecord;

namespace AuditService.Application.FunctionalAuditing.Ingestion;

public interface IAuditRecordMapper
{
    CreateAuditRecordCommand Map(AuditRecordEnvelope envelope);
}
