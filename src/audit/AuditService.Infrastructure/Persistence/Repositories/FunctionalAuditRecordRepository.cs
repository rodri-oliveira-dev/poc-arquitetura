using AuditService.Application.Abstractions.Persistence;
using AuditService.Domain.FunctionalAuditing;

using Microsoft.EntityFrameworkCore;

using Npgsql;

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
        => SaveChangesCoreAsync(cancellationToken);

    private async Task SaveChangesCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyKeyUniqueViolation(exception))
        {
            DetachPendingFunctionalAuditRecords();
            throw new IdempotencyKeyUniqueConstraintViolationException(exception);
        }
    }

    private void DetachPendingFunctionalAuditRecords()
    {
        foreach (var entry in dbContext.ChangeTracker
            .Entries<FunctionalAuditRecord>()
            .Where(static entry => entry.State is EntityState.Added))
        {
            entry.State = EntityState.Detached;
        }
    }

    private static bool IsIdempotencyKeyUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(
                postgresException.ConstraintName,
                "ux_audit_functional_audit_records_idempotency_key",
                StringComparison.Ordinal);
}
