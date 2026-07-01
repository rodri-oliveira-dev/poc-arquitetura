namespace AuditService.Application.FunctionalAuditing.Ingestion;

public interface IAuditRecordSerializer
{
    string Serialize(AuditRecordEnvelope envelope);

    AuditRecordEnvelope Deserialize(string json);
}
