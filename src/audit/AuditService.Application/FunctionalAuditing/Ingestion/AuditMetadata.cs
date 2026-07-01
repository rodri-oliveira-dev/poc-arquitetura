namespace AuditService.Application.FunctionalAuditing.Ingestion;

public sealed record AuditMetadata(
    Guid? CorrelationId,
    string? CausationId,
    IReadOnlyDictionary<string, string>? Attributes);
