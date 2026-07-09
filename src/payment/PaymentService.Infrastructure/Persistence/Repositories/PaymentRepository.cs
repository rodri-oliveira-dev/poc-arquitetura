using Microsoft.EntityFrameworkCore;

using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository(PaymentDbContext context) : IPaymentRepository
{
    private readonly PaymentDbContext _context = context;

    public Task<Payment?> GetByIdAsync(PaymentId paymentId, CancellationToken cancellationToken)
        => _context.Payments
            .FirstOrDefaultAsync(x => x.PaymentId == paymentId, cancellationToken);

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payment);
        await _context.Payments.AddAsync(payment, cancellationToken);
    }
}
