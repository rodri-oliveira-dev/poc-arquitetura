namespace AuditService.Api.Contracts;

public sealed record CreateAuditRecordActorRequest(
    string? Type,
    string? Subject,
    string? ClientId);
