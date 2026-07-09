namespace PaymentService.Api.Contracts.Responses;

public sealed record CreatePaymentResponse(
    Guid PaymentId,
    string Status,
    string MerchantId,
    decimal Amount,
    string Currency,
    string? ExternalReference,
    string StatusUrl);
