using IdentityService.Application.Idempotency;
using IdentityService.Application.Idempotency.Ports;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using Npgsql;

namespace IdentityService.Infrastructure.Persistence.Repositories;

public sealed class IdempotencyRepository(IdentityDbContext context) : IIdempotencyRepository
{
    public Task<IdempotencyRecord?> GetByOperationAndKeyAsync(
        string operationName,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        => context.IdempotencyRecords
            .FirstOrDefaultAsync(
                x => x.OperationName == operationName && x.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public async Task<bool> TryAddProcessingAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await context.IdempotencyRecords.AddAsync(record, cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsIdempotencyUniqueViolation(exception))
        {
            context.Entry(record).State = EntityState.Detached;
            return false;
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);

    public Task<int> SaveFailureAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        foreach (var entry in context.ChangeTracker.Entries().ToArray())
        {
            if (ReferenceEquals(entry.Entity, record))
                continue;

            DetachPendingOrFailedChange(entry);
        }

        return context.SaveChangesAsync(cancellationToken);
    }

    private static void DetachPendingOrFailedChange(EntityEntry entry)
    {
        if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            entry.State = EntityState.Detached;
    }

    private static bool IsIdempotencyUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(
                postgresException.ConstraintName,
                "ux_identity_idempotency_records_operation_key",
                StringComparison.Ordinal);
}
