using PaymentService.Application.Payments.Webhooks;

namespace PaymentService.Application.Payments.InboxProcessing;

public sealed record ProcessPaymentInboxMessageResult(
    Guid InboxMessageId,
    PaymentInboxStatus Status,
    string Outcome,
    bool PaymentChanged);
