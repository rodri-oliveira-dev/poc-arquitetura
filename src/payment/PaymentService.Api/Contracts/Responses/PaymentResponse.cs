namespace PaymentService.Api.Contracts.Responses;

public sealed record PaymentResponse(
    Guid PaymentId,
    string Status,
    string MerchantId,
    decimal Amount,
    string Currency,
    string? Description,
    string? ExternalReference,
    string? ExternalPaymentReference,
    Guid? LedgerEntryId,
    string? ProviderStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);
