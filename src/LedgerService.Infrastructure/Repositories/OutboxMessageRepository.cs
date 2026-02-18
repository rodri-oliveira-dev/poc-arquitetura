using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using System;

namespace LedgerService.Infrastructure.Repositories;

public sealed class OutboxMessageRepository : IOutboxMessageRepository
{
    private readonly AppDbContext _context;

    public OutboxMessageRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken = default)
    {
        await _context.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
        int batchSize,
        DateTime now,
        string lockOwner,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default)
    {
        // Fallback para testes (InMemory provider) ou cenários não-relacionais.
        if (!_context.Database.IsRelational())
        {
            var lockedUntilFallback = now.Add(lockDuration);

            var candidates = await _context.OutboxMessages
                .Where(x =>
                    x.Status == OutboxStatus.Pending &&
                    (x.NextAttemptAt == null || x.NextAttemptAt <= now) &&
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

        // Importante: para evitar dois publishers processarem a mesma linha simultaneamente,
        // usamos um UPDATE com subquery + FOR UPDATE SKIP LOCKED.
        // Isso é específico do PostgreSQL.

        DateTime lockedUntil = now.Add(lockDuration);

        var sql = @"
WITH cte AS (
    SELECT id
    FROM outbox_messages
    WHERE (
            status = @p_pending
            OR (status = @p_processing AND locked_until IS NOT NULL AND locked_until <= @p_now)
          )
      AND (next_attempt_at IS NULL OR next_attempt_at <= @p_now)
      AND (locked_until IS NULL OR locked_until <= @p_now)
    ORDER BY occurred_at
    FOR UPDATE SKIP LOCKED
    LIMIT @p_batch
)
UPDATE outbox_messages o
SET status = @p_processing,
    lock_owner = @p_lock_owner,
    locked_until = @p_locked_until
FROM cte
WHERE o.id = cte.id
RETURNING o.*;
";

        // O FromSqlRaw retorna entidades rastreadas pelo DbContext.
        // Garantimos que esse método seja chamado dentro de um escopo por ciclo do BackgroundService.
        var claimed = await _context.OutboxMessages.FromSqlRaw(
            sql,
            new NpgsqlParameter("p_pending", NpgsqlDbType.Text) { Value = OutboxStatus.Pending.ToString() },
            new NpgsqlParameter("p_processing", NpgsqlDbType.Text) { Value = OutboxStatus.Processing.ToString() },
            new NpgsqlParameter("p_now", NpgsqlDbType.Timestamp) { Value = now },                // UTC
            new NpgsqlParameter("p_locked_until", NpgsqlDbType.Timestamp) { Value = lockedUntil },// UTC
            new NpgsqlParameter("p_lock_owner", NpgsqlDbType.Text) { Value = lockOwner },
            new NpgsqlParameter("p_batch", NpgsqlDbType.Integer) { Value = batchSize }
        ).ToListAsync(cancellationToken);

        return claimed;
    }

    public async Task MarkSentAsync(Guid id, DateTime processedAt, CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
        {
            var entity = await _context.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity is null)
                throw new InvalidOperationException($"OutboxMessage {id} não encontrada para MarkSent.");

            entity.MarkSent(processedAt);
            return;
        }

        var rows = await _context.OutboxMessages
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, OutboxStatus.Sent)
                .SetProperty(x => x.ProcessedAt, processedAt)
                .SetProperty(x => x.LastError, (string?)null)
                .SetProperty(x => x.LockOwner, (string?)null)
                .SetProperty(x => x.LockedUntil, (DateTime?)null)
                .SetProperty(x => x.NextAttemptAt, (DateTime?)null),
                cancellationToken);

        if (rows == 0)
            throw new InvalidOperationException($"OutboxMessage {id} não encontrada para MarkSent.");
    }

    public async Task MarkFailedAttemptAsync(
        Guid id,
        int maxAttempts,
        DateTime nextAttemptAt,
        string? lastError,
        CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
        {
            var entity = await _context.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity is null)
                throw new InvalidOperationException($"OutboxMessage {id} não encontrada para MarkFailedAttempt.");

            entity.MarkFailedAttempt(maxAttempts, nextAttemptAt, lastError);
            return;
        }

        // Faz uma atualização atômica de attempts + decide status (PENDING ou FAILED)
        // com base no attempts atual.
        // Como o attempts é incrementado no banco, não precisamos carregar a entidade.

        var sql = @"
UPDATE outbox_messages
SET attempts = attempts + 1,
    last_error = {0},
    lock_owner = NULL,
    locked_until = NULL,
    status = CASE WHEN (attempts + 1) >= {1} THEN {2} ELSE {3} END,
    next_attempt_at = CASE WHEN (attempts + 1) >= {1} THEN NULL ELSE {4} END,
    processed_at = CASE WHEN (attempts + 1) >= {1} THEN {5} ELSE processed_at END
WHERE id = {6};
";

        var affected = await _context.Database.ExecuteSqlRawAsync(
            sql,
            lastError ?? (object)DBNull.Value,
            maxAttempts,
            OutboxStatus.Failed.ToString(),
            OutboxStatus.Pending.ToString(),
            nextAttemptAt,
            DateTime.Now,
            id);

        if (affected == 0)
            throw new InvalidOperationException($"OutboxMessage {id} não encontrada para MarkFailedAttempt.");
    }
}