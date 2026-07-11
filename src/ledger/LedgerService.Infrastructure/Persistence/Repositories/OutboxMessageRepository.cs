using LedgerService.Application.Abstractions.Messaging;
using LedgerService.Application.Abstractions.Time;
using LedgerService.Infrastructure.Observability;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using NpgsqlTypes;

namespace LedgerService.Infrastructure.Persistence.Repositories;

public sealed class OutboxMessageRepository : IOutboxMessageRepository
{
    private readonly AppDbContext _context;
    private readonly OutboxMetrics? _metrics;
    private readonly IClock _clock;

    public OutboxMessageRepository(AppDbContext context, IClock? clock = null, OutboxMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _clock = clock ?? new SystemClock();
        _metrics = metrics;
    }

    public async Task AddAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outboxMessage);

        await _context.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
        _metrics?.RecordMessageCreated(outboxMessage.EventType);
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
        int batchSize,
        DateTime now,
        string lockOwner,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lockOwner);

        if (!_context.Database.IsRelational())
        {
            var lockedUntilFallback = now.Add(lockDuration);

            var candidates = await _context.OutboxMessages
                .Where(x =>
                    x.Status == OutboxStatus.Pending &&
                    (x.NextRetryAt == null || x.NextRetryAt <= now) &&
                    (x.LockedUntil == null || x.LockedUntil <= now))
                .OrderBy(x => x.OccurredAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            foreach (var msg in candidates)
            {
                msg.MarkProcessing(lockOwner, lockedUntilFallback);
            }

            return candidates;
        }

        DateTime lockedUntil = now.Add(lockDuration);

        var sql = @"
WITH cte AS (
    SELECT id
    FROM ledger.outbox_messages
    WHERE (
            status = @p_pending
            OR (status = @p_processing AND locked_until IS NOT NULL AND locked_until <= @p_now)
          )
      AND (next_retry_at IS NULL OR next_retry_at <= @p_now)
      AND (locked_until IS NULL OR locked_until <= @p_now)
    ORDER BY occurred_at
    FOR UPDATE SKIP LOCKED
    LIMIT @p_batch
)
UPDATE ledger.outbox_messages o
SET status = @p_processing,
    lock_owner = @p_lock_owner,
    locked_until = @p_locked_until
FROM cte
WHERE o.id = cte.id
RETURNING o.*;
";

        var claimed = await _context.OutboxMessages.FromSqlRaw(
            sql,
            new NpgsqlParameter("p_pending", NpgsqlDbType.Text) { Value = OutboxStatus.Pending.ToString() },
            new NpgsqlParameter("p_processing", NpgsqlDbType.Text) { Value = OutboxStatus.Processing.ToString() },
            new NpgsqlParameter("p_now", NpgsqlDbType.TimestampTz) { Value = now },
            new NpgsqlParameter("p_locked_until", NpgsqlDbType.TimestampTz) { Value = lockedUntil },
            new NpgsqlParameter("p_lock_owner", NpgsqlDbType.Text) { Value = lockOwner },
            new NpgsqlParameter("p_batch", NpgsqlDbType.Integer) { Value = batchSize }
        ).ToListAsync(cancellationToken);

        return claimed;
    }

    public async Task MarkProcessedAsync(Guid id, DateTime processedAt, CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
        {
            var entity = await _context.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new InvalidOperationException($"OutboxMessage {id} nao encontrada para MarkProcessed.");

            entity.MarkProcessed(processedAt);
            return;
        }

        var rows = await _context.OutboxMessages
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, OutboxStatus.Processed)
                .SetProperty(x => x.ProcessedAt, processedAt)
                .SetProperty(x => x.LastError, (string?)null)
                .SetProperty(x => x.LockOwner, (string?)null)
                .SetProperty(x => x.LockedUntil, (DateTime?)null)
                .SetProperty(x => x.NextRetryAt, (DateTime?)null),
                cancellationToken);

        if (rows == 0)
            throw new InvalidOperationException($"OutboxMessage {id} nao encontrada para MarkProcessed.");
    }

    public async Task<OutboxStatus> MarkFailedPublishAttemptAsync(
        Guid id,
        int maxRetries,
        DateTime nextRetryAt,
        string? lastError,
        CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
        {
            var entity = await _context.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new InvalidOperationException($"OutboxMessage {id} nao encontrada para MarkFailedPublishAttempt.");

            entity.MarkFailedPublishAttempt(maxRetries, nextRetryAt, lastError);
            return entity.Status;
        }

        var sql = @"
UPDATE ledger.outbox_messages
SET retry_count = retry_count + 1,
    last_error = @p_last_error,
    lock_owner = NULL,
    locked_until = NULL,
    status = CASE WHEN (retry_count + 1) >= @p_max_retries THEN @p_dead_letter ELSE @p_pending END,
    next_retry_at = CASE WHEN (retry_count + 1) >= @p_max_retries THEN NULL ELSE @p_next_retry_at END,
    processed_at = CASE WHEN (retry_count + 1) >= @p_max_retries THEN @p_processed_at ELSE processed_at END
WHERE id = @p_id;
";

        var affected = await _context.Database.ExecuteSqlRawAsync(
            sql,
            new NpgsqlParameter("p_last_error", NpgsqlDbType.Text) { Value = lastError ?? (object)DBNull.Value },
            new NpgsqlParameter("p_max_retries", NpgsqlDbType.Integer) { Value = maxRetries },
            new NpgsqlParameter("p_dead_letter", NpgsqlDbType.Text) { Value = OutboxStatus.DeadLetter.ToString() },
            new NpgsqlParameter("p_pending", NpgsqlDbType.Text) { Value = OutboxStatus.Pending.ToString() },
            new NpgsqlParameter("p_next_retry_at", NpgsqlDbType.TimestampTz) { Value = nextRetryAt },
            new NpgsqlParameter("p_processed_at", NpgsqlDbType.TimestampTz) { Value = _clock.UtcNow.UtcDateTime },
            new NpgsqlParameter("p_id", NpgsqlDbType.Uuid) { Value = id });

        return affected == 0
            ? throw new InvalidOperationException($"OutboxMessage {id} nao encontrada para MarkFailedPublishAttempt.")
            : await _context.OutboxMessages
            .Where(x => x.Id == id)
            .Select(x => x.Status)
            .SingleAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<OutboxMessage> Items, int TotalCount)> GetDeadLettersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var skip = (Math.Max(1, page) - 1) * pageSize;

        var query = _context.OutboxMessages
            .AsNoTracking()
            .Where(x => x.Status == OutboxStatus.DeadLetter);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.OccurredAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<OutboxMessage>> RequeueDeadLettersAsync(
        Guid? id,
        string? eventType,
        DateTime? occurredFrom,
        DateTime? occurredUntil,
        int limit,
        DateTime requeuedAt,
        string requeuedBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requeuedBy);
        ArgumentNullException.ThrowIfNull(reason);

        IQueryable<OutboxMessage> query = _context.OutboxMessages
            .Where(x => x.Status == OutboxStatus.DeadLetter);

        if (id is not null)
            query = query.Where(x => x.Id == id);

        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(x => x.EventType == eventType);

        if (occurredFrom is not null)
            query = query.Where(x => x.OccurredAt >= occurredFrom);

        if (occurredUntil is not null)
            query = query.Where(x => x.OccurredAt <= occurredUntil);

        var messages = await query
            .OrderBy(x => x.OccurredAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.RequeueDeadLetter(requeuedAt, requeuedBy, reason);
        }

        return messages;
    }
}
