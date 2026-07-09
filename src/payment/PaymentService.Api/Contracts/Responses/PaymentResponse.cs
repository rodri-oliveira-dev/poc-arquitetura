namespace PaymentService.Api.Contracts.Responses;

public sealed record PaymentResponse(
    Guid PaymentId,
    string Status,
    string MerchantId,
    decimal Amount,
    string Currency,
    string Provider,
    string? Description,
    string? ExternalReference,
    string? ProviderPaymentId,
    Guid? LedgerEntryId,
    string? ProviderStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);
