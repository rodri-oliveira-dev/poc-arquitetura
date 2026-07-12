namespace PaymentService.Application.Abstractions.Gateway;

public sealed record CreateExternalRefundResult(
    string Provider,
    string ProviderRefundId,
    string ProviderPaymentId,
    string ProviderStatus,
    decimal Amount,
    string Currency,
    DateTimeOffset CreatedAt,
    string? RawStatus);
