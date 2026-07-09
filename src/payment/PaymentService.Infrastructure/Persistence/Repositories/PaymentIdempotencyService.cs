using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Abstractions.Time;
using PaymentService.Application.Payments.Commands;

namespace PaymentService.Infrastructure.Persistence.Repositories;

public sealed class PaymentIdempotencyService(PaymentDbContext context, IClock clock) : IPaymentIdempotencyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PaymentDbContext _context = context;
    private readonly IClock _clock = clock;

    public async Task<PaymentIdempotencyEntry?> GetAsync(
        string merchantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var record = await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.MerchantId == merchantId
                    && x.IdempotencyKey == idempotencyKey
                    && x.ExpiresAt > now,
                cancellationToken);

        if (record is null)
            return null;

        var response = JsonSerializer.Deserialize<CreatePaymentResult>(record.ResponseBody, JsonOptions)
            ?? throw new InvalidOperationException("Idempotency response body invalido.");

        return new PaymentIdempotencyEntry(record.RequestHash, response);
    }

    public async Task AddAsync(
        string merchantId,
        string idempotencyKey,
        string requestHash,
        CreatePaymentResult response,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var record = new PaymentIdempotencyRecord(
            Guid.NewGuid(),
            merchantId,
            idempotencyKey,
            requestHash,
            JsonSerializer.Serialize(response, JsonOptions),
            _clock.UtcNow,
            expiresAt);

        await _context.IdempotencyRecords.AddAsync(record, cancellationToken);
    }
}
