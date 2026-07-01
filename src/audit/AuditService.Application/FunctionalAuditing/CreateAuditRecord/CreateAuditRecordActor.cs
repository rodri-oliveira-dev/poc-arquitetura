namespace AuditService.Application.FunctionalAuditing.CreateAuditRecord;

public sealed record CreateAuditRecordActor(
    string? Type,
    string? Subject,
    string? ClientId);
