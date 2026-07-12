using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

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

        if (IsRefundEvent(inboxMessage.EventType))
            return MapRefundEvent(inboxMessage);

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

    private static ProviderEventMappingResult MapRefundEvent(PaymentInboxMessage inboxMessage)
    {
        try
        {
            using var document = JsonDocument.Parse(inboxMessage.Payload);
            var root = document.RootElement;

            if (!root.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("object", out var obj) ||
                obj.ValueKind != JsonValueKind.Object ||
                !TryGetString(obj, "id", out var providerRefundId))
            {
                return ProviderEventMappingResult.PermanentFailure("Required provider refund identifier missing.");
            }

            var paymentIntent = TryGetString(obj, "payment_intent", out var providerPaymentId)
                ? providerPaymentId
                : inboxMessage.ProviderPaymentId;
            if (string.IsNullOrWhiteSpace(paymentIntent))
                return ProviderEventMappingResult.PermanentFailure("Required provider payment identifier missing for refund.");

            var refundId = TryReadRefundId(obj);
            if (refundId is null)
                return ProviderEventMappingResult.PermanentFailure("Required internal refund identifier missing.");

            var status = TryGetString(obj, "status", out var rawStatus)
                ? rawStatus
                : ResolveProviderStatus(inboxMessage.EventType);
            var kind = inboxMessage.EventType switch
            {
                "refund.failed" => PaymentProviderEventKind.RefundFailed,
                "refund.updated" when string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase) => PaymentProviderEventKind.RefundSucceeded,
                "refund.updated" when string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase) => PaymentProviderEventKind.RefundFailed,
                "refund.updated" => PaymentProviderEventKind.RefundCreated,
                "refund.created" when string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase) => PaymentProviderEventKind.RefundSucceeded,
                _ => PaymentProviderEventKind.RefundCreated
            };

            var amount = obj.TryGetProperty("amount", out var amountProperty) && amountProperty.ValueKind == JsonValueKind.Number
                ? amountProperty.GetInt64() / 100m
                : (decimal?)null;
            var currency = TryGetString(obj, "currency", out var rawCurrency)
                ? rawCurrency.ToUpperInvariant()
                : null;

            return ProviderEventMappingResult.Success(new PaymentProviderEvent(
                inboxMessage.Provider,
                inboxMessage.ProviderEventId,
                inboxMessage.EventType,
                kind,
                inboxMessage.PaymentId,
                new ExternalPaymentReference(paymentIntent),
                status,
                inboxMessage.CorrelationId,
                refundId,
                providerRefundId,
                amount,
                currency));
        }
        catch (JsonException)
        {
            return ProviderEventMappingResult.PermanentFailure("Invalid refund event payload.");
        }
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

    private static bool IsRefundEvent(string eventType)
        => eventType is "refund.created" or "refund.updated" or "refund.failed";

    private static RefundId? TryReadRefundId(JsonElement obj)
    {
        if (!obj.TryGetProperty("metadata", out var metadata) ||
            metadata.ValueKind != JsonValueKind.Object ||
            !TryGetString(metadata, "refund_id", out var refundIdRaw) ||
            !Guid.TryParse(refundIdRaw, out var refundId))
        {
            return null;
        }

        return new RefundId(refundId);
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return false;

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }
}
