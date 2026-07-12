using PaymentService.Application.Abstractions.Persistence;

namespace PaymentService.Application.Payments.Webhooks;

public sealed record ReceiveStripeWebhookResult(
    PaymentInboxStoreResult StoreResult,
    PaymentInboxStatus InboxStatus,
    StripeWebhookEventCategory EventCategory,
    string EventType);
