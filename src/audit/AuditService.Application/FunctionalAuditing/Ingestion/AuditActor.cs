namespace AuditService.Application.FunctionalAuditing.Ingestion;

public sealed record AuditActor(
    string? Type,
    string? Subject,
    string? ClientId);
