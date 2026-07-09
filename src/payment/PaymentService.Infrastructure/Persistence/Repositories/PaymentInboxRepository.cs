using Microsoft.EntityFrameworkCore;

using Npgsql;

using NpgsqlTypes;

using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Payments.Webhooks;

namespace PaymentService.Infrastructure.Persistence.Repositories;

public sealed class PaymentInboxRepository(PaymentDbContext context) : IPaymentInboxRepository
{
    private const string UniqueConstraintName = "ux_payment_inbox_provider_event";

    private readonly PaymentDbContext _context = context;

    public async Task<PaymentInboxStoreResult> StoreAsync(
        PaymentInboxMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        await _context.InboxMessages.AddAsync(message, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return PaymentInboxStoreResult.Inserted;
        }
        catch (DbUpdateException exception) when (IsDuplicateInboxMessage(exception))
        {
            _context.Entry(message).State = EntityState.Detached;
            return PaymentInboxStoreResult.Duplicate;
        }
    }

    public async Task<IReadOnlyList<PaymentInboxMessage>> ClaimEligibleAsync(
        int batchSize,
        DateTimeOffset now,
        string lockOwner,
        TimeSpan leaseTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentNullException.ThrowIfNull(lockOwner);

        var lockedUntil = now.Add(leaseTimeout);

        if (!_context.Database.IsRelational())
        {
            var candidates = await _context.InboxMessages
                .Where(x =>
                    x.Status == PaymentInboxStatus.Pending
                    || (x.Status == PaymentInboxStatus.RetryScheduled && x.NextRetryAt <= now)
                    || (x.Status == PaymentInboxStatus.Processing && x.LockedUntil <= now))
                .OrderBy(x => x.ReceivedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            foreach (var candidate in candidates)
            {
                candidate.MarkProcessing(lockOwner, now, lockedUntil);
            }

            return candidates;
        }

        const string sql = """
            WITH cte AS (
                SELECT id
                FROM payment.inbox_messages
                WHERE status = @p_pending
                   OR (status = @p_retry_scheduled AND next_retry_at_utc IS NOT NULL AND next_retry_at_utc <= @p_now)
                   OR (status = @p_processing AND locked_until_utc IS NOT NULL AND locked_until_utc <= @p_now)
                ORDER BY received_at_utc
                FOR UPDATE SKIP LOCKED
                LIMIT @p_batch
            )
            UPDATE payment.inbox_messages i
            SET status = @p_processing,
                attempt_count = attempt_count + 1,
                processing_started_at_utc = @p_now,
                lock_owner = @p_lock_owner,
                locked_until_utc = @p_locked_until,
                next_retry_at_utc = NULL,
                last_error = NULL,
                updated_at = @p_now
            FROM cte
            WHERE i.id = cte.id
            RETURNING i.*;
            """;

        return await _context.InboxMessages.FromSqlRaw(
                sql,
                new NpgsqlParameter("p_pending", NpgsqlDbType.Text) { Value = PaymentInboxStatus.Pending.ToString() },
                new NpgsqlParameter("p_retry_scheduled", NpgsqlDbType.Text) { Value = PaymentInboxStatus.RetryScheduled.ToString() },
                new NpgsqlParameter("p_processing", NpgsqlDbType.Text) { Value = PaymentInboxStatus.Processing.ToString() },
                new NpgsqlParameter("p_now", NpgsqlDbType.TimestampTz) { Value = now },
                new NpgsqlParameter("p_locked_until", NpgsqlDbType.TimestampTz) { Value = lockedUntil },
                new NpgsqlParameter("p_lock_owner", NpgsqlDbType.Text) { Value = lockOwner },
                new NpgsqlParameter("p_batch", NpgsqlDbType.Integer) { Value = batchSize })
            .ToListAsync(cancellationToken);
    }

    public Task<PaymentInboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _context.InboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<PaymentInboxStatus> MarkFailedProcessingAttemptAsync(
        Guid id,
        int maxRetryCount,
        DateTimeOffset now,
        DateTimeOffset nextRetryAt,
        string lastError,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lastError);

        if (!_context.Database.IsRelational())
        {
            var entity = await GetRequiredAsync(id, cancellationToken);
            if (entity.AttemptCount >= maxRetryCount)
                entity.MarkDeadLetter(now, lastError);
            else
                entity.ScheduleRetry(now, nextRetryAt, lastError);

            return entity.Status;
        }

        const string sql = """
            UPDATE payment.inbox_messages
            SET status = CASE WHEN attempt_count >= @p_max_retry_count THEN @p_dead_letter ELSE @p_retry_scheduled END,
                processed_at_utc = CASE WHEN attempt_count >= @p_max_retry_count THEN @p_now ELSE NULL END,
                next_retry_at_utc = CASE WHEN attempt_count >= @p_max_retry_count THEN NULL ELSE @p_next_retry_at END,
                last_error = @p_last_error,
                processing_started_at_utc = NULL,
                lock_owner = NULL,
                locked_until_utc = NULL,
                updated_at = @p_now
            WHERE id = @p_id;
            """;

        var affected = await _context.Database.ExecuteSqlRawAsync(
            sql,
            new NpgsqlParameter("p_dead_letter", NpgsqlDbType.Text) { Value = PaymentInboxStatus.DeadLetter.ToString() },
            new NpgsqlParameter("p_retry_scheduled", NpgsqlDbType.Text) { Value = PaymentInboxStatus.RetryScheduled.ToString() },
            new NpgsqlParameter("p_now", NpgsqlDbType.TimestampTz) { Value = now },
            new NpgsqlParameter("p_next_retry_at", NpgsqlDbType.TimestampTz) { Value = nextRetryAt },
            new NpgsqlParameter("p_last_error", NpgsqlDbType.Text) { Value = SanitizeError(lastError) },
            new NpgsqlParameter("p_max_retry_count", NpgsqlDbType.Integer) { Value = maxRetryCount },
            new NpgsqlParameter("p_id", NpgsqlDbType.Uuid) { Value = id });

        return affected == 0
            ? throw new InvalidOperationException($"Payment InboxMessage {id} nao encontrada.")
            : await _context.InboxMessages
            .Where(x => x.Id == id)
            .Select(x => x.Status)
            .SingleAsync(cancellationToken);
    }

    public Task<int> CountBacklogAsync(DateTimeOffset now, CancellationToken cancellationToken)
        => _context.InboxMessages.CountAsync(
            x => x.Status == PaymentInboxStatus.Pending
                || (x.Status == PaymentInboxStatus.RetryScheduled && x.NextRetryAt <= now)
                || x.Status == PaymentInboxStatus.Processing
                || x.Status == PaymentInboxStatus.DeadLetter,
            cancellationToken);

    private static bool IsDuplicateInboxMessage(DbUpdateException exception)
        => exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(postgresException.ConstraintName, UniqueConstraintName, StringComparison.Ordinal);

    private async Task<PaymentInboxMessage> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
        => await _context.InboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Payment InboxMessage {id} nao encontrada.");

    private static string SanitizeError(string lastError)
        => lastError.Length <= PaymentInboxMessage.LastErrorMaxLength
            ? lastError
            : lastError[..PaymentInboxMessage.LastErrorMaxLength];
}
