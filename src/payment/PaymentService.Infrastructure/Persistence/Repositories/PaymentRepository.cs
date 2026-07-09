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

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payment);
        await _context.Payments.AddAsync(payment, cancellationToken);
    }
}
