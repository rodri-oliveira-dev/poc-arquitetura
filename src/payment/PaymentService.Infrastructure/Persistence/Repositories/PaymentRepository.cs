using Microsoft.EntityFrameworkCore;

using Npgsql;

using NpgsqlTypes;

using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository(PaymentDbContext context) : IPaymentRepository
{
    private readonly PaymentDbContext _context = context;

    public Task<Payment?> GetByIdAsync(PaymentId paymentId, CancellationToken cancellationToken)
        => _context.Payments
            .FirstOrDefaultAsync(x => x.PaymentId == paymentId, cancellationToken);

    public Task<Payment?> GetByIdForUpdateAsync(PaymentId paymentId, CancellationToken cancellationToken)
    {
        return !_context.Database.IsRelational()
            ? GetByIdAsync(paymentId, cancellationToken)
            : _context.Payments
            .FromSqlRaw(
                "SELECT * FROM payment.payments WHERE id = @p_id FOR UPDATE",
                new NpgsqlParameter("p_id", NpgsqlDbType.Uuid) { Value = paymentId.Value })
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Payment?> GetByProviderReferenceForUpdateAsync(
        PaymentProvider provider,
        ExternalPaymentReference externalPaymentReference,
        CancellationToken cancellationToken)
    {
        return !_context.Database.IsRelational()
            ? _context.Payments.FirstOrDefaultAsync(
                x => x.Provider == provider && x.ExternalPaymentReference == externalPaymentReference,
                cancellationToken)
            : _context.Payments
            .FromSqlRaw(
                """
                SELECT *
                FROM payment.payments
                WHERE provider = @p_provider
                  AND external_payment_reference = @p_external_payment_reference
                FOR UPDATE
                """,
                new NpgsqlParameter("p_provider", NpgsqlDbType.Text) { Value = provider.ToString() },
                new NpgsqlParameter("p_external_payment_reference", NpgsqlDbType.Text) { Value = externalPaymentReference.Value })
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> ClaimLedgerIntegrationAsync(
        int batchSize,
        DateTimeOffset now,
        string lockOwner,
        TimeSpan leaseTimeout,
        CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational())
        {
            var candidates = await _context.Payments
                .Where(x =>
                    x.LedgerEntryReference == null &&
                    (x.Status == PaymentStatus.Succeeded || x.Status == PaymentStatus.LedgerPending) &&
                    x.LedgerIntegrationStatus != LedgerIntegrationStatus.FailedDefinitive &&
                    x.LedgerIntegrationStatus != LedgerIntegrationStatus.DeadLetter &&
                    (x.LedgerNextRetryAt == null || x.LedgerNextRetryAt <= now) &&
                    (x.LedgerLockedUntil == null || x.LedgerLockedUntil <= now))
                .OrderBy(x => x.UpdatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            foreach (var payment in candidates)
            {
                payment.ClaimLedgerIntegration(now, lockOwner, now.Add(leaseTimeout));
            }

            await _context.SaveChangesAsync(cancellationToken);
            return candidates;
        }

        var lockedUntil = now.Add(leaseTimeout);
        return await _context.Payments
            .FromSqlRaw(
                """
                UPDATE payment.payments
                SET status = 'LedgerPending',
                    ledger_integration_status = 'Processing',
                    ledger_integration_attempt_count = ledger_integration_attempt_count + 1,
                    ledger_processing_started_at_utc = @p_now,
                    ledger_locked_until_utc = @p_locked_until,
                    ledger_lock_owner = @p_lock_owner,
                    ledger_last_error = NULL,
                    updated_at = @p_now
                WHERE id IN (
                    SELECT id
                    FROM payment.payments
                    WHERE ledger_entry_id IS NULL
                      AND status IN ('Succeeded', 'LedgerPending')
                      AND ledger_integration_status NOT IN ('FailedDefinitive', 'DeadLetter', 'Completed')
                      AND (ledger_next_retry_at_utc IS NULL OR ledger_next_retry_at_utc <= @p_now)
                      AND (ledger_locked_until_utc IS NULL OR ledger_locked_until_utc <= @p_now)
                    ORDER BY updated_at, id
                    LIMIT @p_batch_size
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING *
                """,
                new NpgsqlParameter("p_now", NpgsqlDbType.TimestampTz) { Value = now },
                new NpgsqlParameter("p_locked_until", NpgsqlDbType.TimestampTz) { Value = lockedUntil },
                new NpgsqlParameter("p_lock_owner", NpgsqlDbType.Text) { Value = lockOwner },
                new NpgsqlParameter("p_batch_size", NpgsqlDbType.Integer) { Value = batchSize })
            .AsTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payment);
        await _context.Payments.AddAsync(payment, cancellationToken);
    }
}
