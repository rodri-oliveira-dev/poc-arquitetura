using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.InboxProcessing;

public sealed record PaymentProviderEvent(
    PaymentProvider Provider,
    string ProviderEventId,
    string EventType,
    PaymentProviderEventKind Kind,
    PaymentId? PaymentId,
    ExternalPaymentReference ProviderPaymentReference,
    string ProviderStatus,
    string? CorrelationId,
    RefundId? RefundId = null,
    string? ProviderRefundId = null,
    decimal? RefundAmount = null,
    string? RefundCurrency = null);
