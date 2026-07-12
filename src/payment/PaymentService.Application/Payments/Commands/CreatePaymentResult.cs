namespace PaymentService.Application.Payments.Commands;

public sealed record CreatePaymentResult(
    Guid PaymentId,
    string Status,
    string MerchantId,
    decimal Amount,
    string Currency,
    string Provider,
    string? Description,
    string? ExternalReference,
    string? ExternalPaymentReference,
    string? ProviderStatus,
    string? ClientSecret,
    Guid? LedgerEntryId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IdempotentReplay);
