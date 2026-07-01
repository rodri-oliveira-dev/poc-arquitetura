using AuditService.Domain.FunctionalAuditing;

namespace AuditService.Application.Abstractions.Persistence;

public interface IFunctionalAuditRecordRepository
{
    Task<FunctionalAuditRecord?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task AddAsync(FunctionalAuditRecord record, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
