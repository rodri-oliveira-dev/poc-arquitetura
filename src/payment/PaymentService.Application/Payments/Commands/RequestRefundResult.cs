namespace PaymentService.Application.Payments.Commands;

public sealed record RequestRefundResult(
    Guid PaymentId,
    Guid RefundId,
    string Status,
    decimal Amount,
    string Currency,
    string Reason,
    string? ExternalReference,
    string? ProviderRefundId,
    string? ProviderStatus,
    Guid? LedgerReversalId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IdempotentReplay);
