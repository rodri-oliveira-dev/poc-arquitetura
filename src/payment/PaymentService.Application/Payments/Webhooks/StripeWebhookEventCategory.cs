namespace PaymentService.Application.Payments.Webhooks;

public enum StripeWebhookEventCategory
{
    Supported = 1,
    KnownUnsupported = 2,
    Unknown = 3
}
