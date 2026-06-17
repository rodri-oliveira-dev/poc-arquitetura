using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using TransferService.Application.Abstractions.Persistence;
using TransferService.Domain.Sagas;

namespace TransferService.Infrastructure.Persistence.Repositories;

public sealed class TransferenciaSagaRepository : ITransferenciaSagaRepository
{
    private readonly TransferServiceDbContext _context;

    public TransferenciaSagaRepository(TransferServiceDbContext context)
    {
        _context = context;
    }

    public Task<TransferenciaSaga?> GetByIdAsync(Guid transferenciaId, CancellationToken cancellationToken)
        => _context.TransferenciasSagas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == transferenciaId, cancellationToken);

    public async Task AddAsync(TransferenciaSaga saga, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(saga);
        await _context.TransferenciasSagas.AddAsync(saga, cancellationToken);
    }

    public async Task<IReadOnlyList<TransferenciaSaga>> ClaimPendingAsync(
        int batchSize,
        DateTimeOffset now,
        string lockOwner,
        TimeSpan lockDuration,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockOwner);
        if (batchSize <= 0)
            return [];

        if (!_context.Database.IsRelational())
        {
            var lockedUntilFallback = now.Add(lockDuration);
            var candidates = await _context.TransferenciasSagas
                .Where(x =>
                    (x.Status == TransferenciaSagaStatus.Pending
                        || x.Status == TransferenciaSagaStatus.Processing
                        || x.Status == TransferenciaSagaStatus.DebitCreating
                        || x.Status == TransferenciaSagaStatus.DebitCreated
                        || x.Status == TransferenciaSagaStatus.CreditCreating
                        || x.Status == TransferenciaSagaStatus.CompensationRequested) &&
                    (x.NextRetryAt == null || x.NextRetryAt <= now) &&
                    (x.ProcessingLockedUntil == null || x.ProcessingLockedUntil <= now))
                .OrderBy(x => x.CreatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            foreach (var saga in candidates)
            {
                saga.MarkClaimedForProcessing(lockOwner, lockedUntilFallback, now);
            }

            return candidates;
        }

        var lockedUntil = now.Add(lockDuration);
        const string sql = @"
WITH cte AS (
    SELECT id
    FROM transfer.transferencias_sagas
    WHERE status IN (@p_pending, @p_processing, @p_debit_creating, @p_debit_created, @p_credit_creating, @p_compensation_requested)
      AND (next_retry_at IS NULL OR next_retry_at <= @p_now)
      AND (processing_locked_until IS NULL OR processing_locked_until <= @p_now)
    ORDER BY created_at
    FOR UPDATE SKIP LOCKED
    LIMIT @p_batch
)
UPDATE transfer.transferencias_sagas s
SET status = CASE WHEN s.status = @p_pending THEN @p_processing ELSE s.status END,
    current_step = CASE WHEN s.status = @p_pending THEN @p_processing_step ELSE s.current_step END,
    processing_lock_owner = @p_lock_owner,
    processing_locked_until = @p_locked_until,
    updated_at = @p_now
FROM cte
WHERE s.id = cte.id
RETURNING s.*;
";

        return await _context.TransferenciasSagas.FromSqlRaw(
            sql,
            new NpgsqlParameter("p_pending", NpgsqlDbType.Text) { Value = TransferenciaSagaStatus.Pending.ToString() },
            new NpgsqlParameter("p_processing", NpgsqlDbType.Text) { Value = TransferenciaSagaStatus.Processing.ToString() },
            new NpgsqlParameter("p_debit_creating", NpgsqlDbType.Text) { Value = TransferenciaSagaStatus.DebitCreating.ToString() },
            new NpgsqlParameter("p_debit_created", NpgsqlDbType.Text) { Value = TransferenciaSagaStatus.DebitCreated.ToString() },
            new NpgsqlParameter("p_credit_creating", NpgsqlDbType.Text) { Value = TransferenciaSagaStatus.CreditCreating.ToString() },
            new NpgsqlParameter("p_compensation_requested", NpgsqlDbType.Text) { Value = TransferenciaSagaStatus.CompensationRequested.ToString() },
            new NpgsqlParameter("p_processing_step", NpgsqlDbType.Text) { Value = TransferenciaSagaStep.Processing.ToString() },
            new NpgsqlParameter("p_now", NpgsqlDbType.TimestampTz) { Value = now },
            new NpgsqlParameter("p_locked_until", NpgsqlDbType.TimestampTz) { Value = lockedUntil },
            new NpgsqlParameter("p_lock_owner", NpgsqlDbType.Text) { Value = lockOwner.Trim() },
            new NpgsqlParameter("p_batch", NpgsqlDbType.Integer) { Value = batchSize })
            .ToListAsync(cancellationToken);
    }
}
