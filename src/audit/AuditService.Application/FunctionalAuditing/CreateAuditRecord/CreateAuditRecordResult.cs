namespace AuditService.Application.FunctionalAuditing.CreateAuditRecord;

public sealed record CreateAuditRecordResult(Guid Id, bool Duplicate = false);
