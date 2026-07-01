using AuditService.Application.Abstractions.Persistence;
using AuditService.Domain.FunctionalAuditing;

using Microsoft.EntityFrameworkCore;

namespace AuditService.Infrastructure.Persistence.Repositories;

public sealed class FunctionalAuditRecordRepository(AuditDbContext dbContext) : IFunctionalAuditRecordRepository
{
    public Task<FunctionalAuditRecord?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKey);

        return dbContext.FunctionalAuditRecords
            .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task AddAsync(FunctionalAuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await dbContext.FunctionalAuditRecords.AddAsync(record, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
