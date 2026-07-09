using System.Diagnostics.CodeAnalysis;

using PaymentService.Application.Payments.Webhooks;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.InboxProcessing;

[SuppressMessage("Style", "IDE0046:Convert to conditional expression", Justification = "Guard clauses keep provider payload failures explicit.")]
public sealed class StripeInboxProviderEventMapper : IProviderEventMapper
{
    public ProviderEventMappingResult Map(PaymentInboxMessage inboxMessage)
    {
        ArgumentNullException.ThrowIfNull(inboxMessage);

        if (inboxMessage.EventCategory != StripeWebhookEventCategory.Supported)
            return ProviderEventMappingResult.PermanentFailure("Unsupported provider event type.");

        if (inboxMessage.Provider != PaymentProvider.Stripe)
            return ProviderEventMappingResult.PermanentFailure("Unsupported payment provider.");

        if (string.IsNullOrWhiteSpace(inboxMessage.ProviderPaymentId))
            return ProviderEventMappingResult.PermanentFailure("Required provider payment identifier missing.");

        return !TryMapKind(inboxMessage.EventType, out var kind)
            ? ProviderEventMappingResult.PermanentFailure("Unsupported provider event type.")
            : ProviderEventMappingResult.Success(new PaymentProviderEvent(
            inboxMessage.Provider,
            inboxMessage.ProviderEventId,
            inboxMessage.EventType,
            kind,
            inboxMessage.PaymentId,
            new ExternalPaymentReference(inboxMessage.ProviderPaymentId),
            ResolveProviderStatus(inboxMessage.EventType),
            inboxMessage.CorrelationId));
    }

    private static bool TryMapKind(string eventType, out PaymentProviderEventKind kind)
    {
        kind = eventType switch
        {
            "payment_intent.processing" => PaymentProviderEventKind.Processing,
            "payment_intent.succeeded" => PaymentProviderEventKind.Succeeded,
            "payment_intent.payment_failed" => PaymentProviderEventKind.Failed,
            "payment_intent.canceled" => PaymentProviderEventKind.Cancelled,
            _ => default
        };

        return eventType is
            "payment_intent.processing" or
            "payment_intent.succeeded" or
            "payment_intent.payment_failed" or
            "payment_intent.canceled";
    }

    private static string ResolveProviderStatus(string eventType)
        => eventType switch
        {
            "payment_intent.payment_failed" => "payment_failed",
            "payment_intent.canceled" => "canceled",
            _ => eventType["payment_intent.".Length..]
        };
}
