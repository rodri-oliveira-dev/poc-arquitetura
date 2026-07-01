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

    public async Task<IdempotencyRecord?> TryClaimExpiredForProcessingAsync(
        string operationName,
        string idempotencyKey,
        string requestHash,
        DateTime nowUtc,
        DateTime expiresAtUtc,
        DateTime? lockedUntilUtc,
        CancellationToken cancellationToken = default)
    {
        var updated = await context.IdempotencyRecords
            .Where(record => record.OperationName == operationName
                && record.IdempotencyKey == idempotencyKey
                && record.ExpiresAtUtc <= nowUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.RequestHash, requestHash)
                .SetProperty(record => record.Status, IdempotencyStatus.Processing)
                .SetProperty(record => record.ResponseStatusCode, (int?)null)
                .SetProperty(record => record.ResponseBody, (string?)null)
                .SetProperty(record => record.ResourceId, (Guid?)null)
                .SetProperty(record => record.CreatedAtUtc, nowUtc)
                .SetProperty(record => record.CompletedAtUtc, (DateTime?)null)
                .SetProperty(record => record.ExpiresAtUtc, expiresAtUtc)
                .SetProperty(record => record.LockedUntilUtc, lockedUntilUtc)
                .SetProperty(record => record.FailureStage, (string?)null)
                .SetProperty(record => record.ErrorMessage, (string?)null),
                cancellationToken);

        return updated == 1
            ? await ReloadClaimedRecordAsync(operationName, idempotencyKey, cancellationToken)
            : null;
    }

    public async Task<IdempotencyRecord?> TryClaimFailedForRetryAsync(
        string operationName,
        string idempotencyKey,
        string requestHash,
        DateTime nowUtc,
        DateTime? lockedUntilUtc,
        CancellationToken cancellationToken = default)
    {
        var updated = await context.IdempotencyRecords
            .Where(record => record.OperationName == operationName
                && record.IdempotencyKey == idempotencyKey
                && record.RequestHash == requestHash
                && record.ExpiresAtUtc > nowUtc
                && record.Status == IdempotencyStatus.Failed
                && (record.FailureStage == IdempotencyFailureStage.BeforeExternalSideEffect
                    || record.FailureStage == IdempotencyFailureStage.AfterIdentityProviderCompensated))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.Status, IdempotencyStatus.Processing)
                .SetProperty(record => record.CompletedAtUtc, (DateTime?)null)
                .SetProperty(record => record.LockedUntilUtc, lockedUntilUtc)
                .SetProperty(record => record.FailureStage, (string?)null)
                .SetProperty(record => record.ErrorMessage, (string?)null),
                cancellationToken);

        return updated == 1
            ? await ReloadClaimedRecordAsync(operationName, idempotencyKey, cancellationToken)
            : null;
    }

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

    private async Task<IdempotencyRecord?> ReloadClaimedRecordAsync(
        string operationName,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();

        return await GetByOperationAndKeyAsync(operationName, idempotencyKey, cancellationToken);
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
