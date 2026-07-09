using Microsoft.EntityFrameworkCore;

using Npgsql;

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

    private static bool IsDuplicateInboxMessage(DbUpdateException exception)
        => exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(postgresException.ConstraintName, UniqueConstraintName, StringComparison.Ordinal);
}
