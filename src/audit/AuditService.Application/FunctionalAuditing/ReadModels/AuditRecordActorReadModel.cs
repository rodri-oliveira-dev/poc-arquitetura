namespace AuditService.Application.FunctionalAuditing.ReadModels;

public sealed record AuditRecordActorReadModel(
    string? Type,
    string? Subject,
    string? ClientId);
