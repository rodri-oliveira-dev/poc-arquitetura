using PaymentService.Domain.Payments;

namespace PaymentService.Api.Webhooks;

public sealed record StripeWebhookValidationResult(
    bool IsValid,
    StripeWebhookValidationFailure? Failure,
    string? ProviderEventId,
    string? EventType,
    string? RawPayload,
    string? ProviderPaymentId,
    PaymentId? PaymentId)
{
    public static StripeWebhookValidationResult Invalid(StripeWebhookValidationFailure failure)
        => new(false, failure, null, null, null, null, null);

    public static StripeWebhookValidationResult Valid(
        string providerEventId,
        string eventType,
        string rawPayload,
        string? providerPaymentId,
        PaymentId? paymentId)
        => new(true, null, providerEventId, eventType, rawPayload, providerPaymentId, paymentId);
}
