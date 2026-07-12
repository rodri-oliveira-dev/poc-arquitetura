using PaymentService.Application.Payments.Commands;

namespace PaymentService.Application.Abstractions.Persistence;

public interface IPaymentIdempotencyService
{
    Task<PaymentIdempotencyEntry?> GetAsync(string merchantId, string idempotencyKey, CancellationToken cancellationToken);

    Task AddAsync(
        string merchantId,
        string idempotencyKey,
        string requestHash,
        CreatePaymentResult response,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    Task UpdateResponseAsync(
        string merchantId,
        string idempotencyKey,
        CreatePaymentResult response,
        CancellationToken cancellationToken);

    Task<PaymentRefundIdempotencyEntry?> GetRefundAsync(string merchantId, string idempotencyKey, CancellationToken cancellationToken);

    Task AddRefundAsync(
        string merchantId,
        string idempotencyKey,
        string requestHash,
        RequestRefundResult response,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    Task UpdateRefundResponseAsync(
        string merchantId,
        string idempotencyKey,
        RequestRefundResult response,
        CancellationToken cancellationToken);
}

public sealed record PaymentRefundIdempotencyEntry(string RequestHash, RequestRefundResult Response);
