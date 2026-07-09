using MediatR;

using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Abstractions.Time;

namespace PaymentService.Application.Payments.Webhooks;

public sealed class ReceiveStripeWebhookCommandHandler(
    IPaymentInboxRepository inboxRepository,
    IClock clock) : IRequestHandler<ReceiveStripeWebhookCommand, ReceiveStripeWebhookResult>
{
    private readonly IPaymentInboxRepository _inboxRepository = inboxRepository;
    private readonly IClock _clock = clock;

    public async Task<ReceiveStripeWebhookResult> Handle(
        ReceiveStripeWebhookCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var message = PaymentInboxMessage.CreateStripe(
            request.ProviderEventId,
            request.EventType,
            request.RawPayload,
            _clock.UtcNow,
            request.CorrelationId,
            request.ProviderPaymentId,
            request.PaymentId);

        var result = await _inboxRepository.StoreAsync(message, cancellationToken);

        return new ReceiveStripeWebhookResult(
            result,
            message.Status,
            message.EventCategory,
            message.EventType);
    }
}
