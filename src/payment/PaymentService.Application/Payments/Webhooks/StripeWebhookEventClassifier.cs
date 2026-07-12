namespace PaymentService.Application.Payments.Webhooks;

public static class StripeWebhookEventClassifier
{
    private static readonly HashSet<string> SupportedEvents = new(StringComparer.Ordinal)
    {
        "payment_intent.processing",
        "payment_intent.succeeded",
        "payment_intent.payment_failed",
        "payment_intent.canceled",
        "refund.created",
        "refund.updated",
        "refund.failed"
    };

    private static readonly string[] KnownUnsupportedPrefixes =
    [
        "charge.refunded",
        "charge.",
        "checkout.",
        "customer.",
        "invoice.",
        "payment_method.",
        "setup_intent."
    ];

    public static StripeWebhookEventCategory Classify(string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            return StripeWebhookEventCategory.Unknown;

        return SupportedEvents.Contains(eventType)
            ? StripeWebhookEventCategory.Supported
            : KnownUnsupportedPrefixes.Any(prefix => eventType.StartsWith(prefix, StringComparison.Ordinal))
            ? StripeWebhookEventCategory.KnownUnsupported
            : StripeWebhookEventCategory.Unknown;
    }
}
