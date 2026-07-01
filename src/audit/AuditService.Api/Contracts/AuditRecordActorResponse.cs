namespace AuditService.Api.Contracts;

public sealed record AuditRecordActorResponse(
    string? Type,
    string? Subject,
    string? ClientId);
