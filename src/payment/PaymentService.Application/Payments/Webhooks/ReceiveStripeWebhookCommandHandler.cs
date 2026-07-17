using MediatR;

using PaymentService.Application.Abstractions.Persistence;

namespace PaymentService.Application.Payments.Webhooks;

public sealed class ReceiveStripeWebhookCommandHandler(
    IPaymentInboxRepository inboxRepository,
    TimeProvider timeProvider) : IRequestHandler<ReceiveStripeWebhookCommand, ReceiveStripeWebhookResult>
{
    private readonly IPaymentInboxRepository _inboxRepository = inboxRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<ReceiveStripeWebhookResult> Handle(
        ReceiveStripeWebhookCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var message = PaymentInboxMessage.CreateStripe(
            request.ProviderEventId,
            request.EventType,
            request.RawPayload,
            _timeProvider.GetUtcNow(),
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
