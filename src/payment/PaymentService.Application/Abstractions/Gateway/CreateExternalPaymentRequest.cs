namespace PaymentService.Application.Abstractions.Gateway;

public sealed record CreateExternalPaymentRequest(
    Guid PaymentId,
    string MerchantId,
    decimal Amount,
    string Currency,
    string? ExternalReference,
    string IdempotencyKey,
    string? CorrelationId);
