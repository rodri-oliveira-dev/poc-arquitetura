namespace AuditService.Application.FunctionalAuditing.Ingestion;

public sealed record AuditIngestionSourceDescriptor(
    string Adapter,
    string? EndpointOrTopic,
    string? ContractName,
    int? ContractVersion);
