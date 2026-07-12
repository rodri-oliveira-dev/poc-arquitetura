namespace PaymentService.Application.Abstractions.Gateway;

public sealed record CreateExternalPaymentResult(
    string Provider,
    string ExternalPaymentReference,
    string ProviderStatus,
    string? ClientSecret,
    bool RequiresAction,
    string? RawStatus);
