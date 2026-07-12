namespace PaymentService.Application.Payments.Queries;

public sealed record PaymentDetailsResult(
    Guid PaymentId,
    string Status,
    string MerchantId,
    decimal Amount,
    string Currency,
    string Provider,
    string? Description,
    string? ExternalReference,
    string? ExternalPaymentReference,
    Guid? LedgerEntryId,
    string? ProviderStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);
