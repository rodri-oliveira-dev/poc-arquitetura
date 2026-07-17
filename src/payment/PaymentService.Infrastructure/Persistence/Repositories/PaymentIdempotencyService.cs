using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Payments.Commands;

namespace PaymentService.Infrastructure.Persistence.Repositories;

public sealed class PaymentIdempotencyService(PaymentDbContext context, TimeProvider timeProvider) : IPaymentIdempotencyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string RefundKeyPrefix = "refund:";

    private readonly PaymentDbContext _context = context;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<PaymentIdempotencyEntry?> GetAsync(
        string merchantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
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
            _timeProvider.GetUtcNow(),
            expiresAt);

        await _context.IdempotencyRecords.AddAsync(record, cancellationToken);
    }

    public async Task UpdateResponseAsync(
        string merchantId,
        string idempotencyKey,
        CreatePaymentResult response,
        CancellationToken cancellationToken)
    {
        var record = await _context.IdempotencyRecords
            .FirstOrDefaultAsync(
                x => x.MerchantId == merchantId && x.IdempotencyKey == idempotencyKey,
                cancellationToken)
            ?? throw new InvalidOperationException("Registro de idempotencia nao encontrado para atualizar resposta.");

        record.UpdateResponse(JsonSerializer.Serialize(response, JsonOptions));
    }

    public async Task<PaymentRefundIdempotencyEntry?> GetRefundAsync(
        string merchantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(merchantId);
        ArgumentNullException.ThrowIfNull(idempotencyKey);

        var now = _timeProvider.GetUtcNow();
        var effectiveKey = BuildRefundKey(idempotencyKey);
        var record = await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.MerchantId == merchantId
                    && x.IdempotencyKey == effectiveKey
                    && x.ExpiresAt > now,
                cancellationToken);

        if (record is null)
            return null;

        var response = JsonSerializer.Deserialize<RequestRefundResult>(record.ResponseBody, JsonOptions)
            ?? throw new InvalidOperationException("Idempotency refund response body invalido.");

        return new PaymentRefundIdempotencyEntry(record.RequestHash, response);
    }

    public async Task AddRefundAsync(
        string merchantId,
        string idempotencyKey,
        string requestHash,
        RequestRefundResult response,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(merchantId);
        ArgumentNullException.ThrowIfNull(idempotencyKey);
        ArgumentNullException.ThrowIfNull(requestHash);
        ArgumentNullException.ThrowIfNull(response);

        var record = new PaymentIdempotencyRecord(
            Guid.NewGuid(),
            merchantId,
            BuildRefundKey(idempotencyKey),
            requestHash,
            JsonSerializer.Serialize(response, JsonOptions),
            _timeProvider.GetUtcNow(),
            expiresAt);

        await _context.IdempotencyRecords.AddAsync(record, cancellationToken);
    }

    public async Task UpdateRefundResponseAsync(
        string merchantId,
        string idempotencyKey,
        RequestRefundResult response,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(merchantId);
        ArgumentNullException.ThrowIfNull(idempotencyKey);
        ArgumentNullException.ThrowIfNull(response);

        var effectiveKey = BuildRefundKey(idempotencyKey);
        var record = await _context.IdempotencyRecords
            .FirstOrDefaultAsync(
                x => x.MerchantId == merchantId && x.IdempotencyKey == effectiveKey,
                cancellationToken)
            ?? throw new InvalidOperationException("Registro de idempotencia de refund nao encontrado para atualizar resposta.");

        record.UpdateResponse(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static string BuildRefundKey(string idempotencyKey)
        => $"{RefundKeyPrefix}{idempotencyKey.Trim()}";
}
