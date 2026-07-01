namespace AuditService.Application.FunctionalAuditing.Ingestion;

public interface IAuditIngestionSource
{
    AuditIngestionSourceDescriptor Describe();
}
