namespace PaymentService.Application.Abstractions.Gateway;

public sealed record CreateExternalRefundRequest(
    Guid PaymentId,
    Guid RefundId,
    string ProviderPaymentId,
    decimal Amount,
    string Currency,
    string Reason,
    string IdempotencyKey,
    string? CorrelationId,
    string? ExternalReference);
