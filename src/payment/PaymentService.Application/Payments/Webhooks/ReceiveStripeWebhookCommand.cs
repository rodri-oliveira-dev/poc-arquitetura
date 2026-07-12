using MediatR;

using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.Webhooks;

public sealed record ReceiveStripeWebhookCommand(
    string ProviderEventId,
    string EventType,
    string RawPayload,
    string? CorrelationId,
    string? ProviderPaymentId,
    PaymentId? PaymentId)
    : IRequest<ReceiveStripeWebhookResult>;
