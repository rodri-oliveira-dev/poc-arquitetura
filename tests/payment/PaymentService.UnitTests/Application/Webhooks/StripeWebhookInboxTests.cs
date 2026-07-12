using Moq;

using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Abstractions.Time;
using PaymentService.Application.Payments.Webhooks;
using PaymentService.Domain.Payments;

namespace PaymentService.UnitTests.Application.Webhooks;

public sealed class StripeWebhookInboxTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);
    private const string Payload = /*lang=json,strict*/ "{\"id\":\"evt_123\"}";

    [Theory]
    [InlineData("payment_intent.processing")]
    [InlineData("payment_intent.succeeded")]
    [InlineData("payment_intent.payment_failed")]
    [InlineData("payment_intent.canceled")]
    public void Classify_should_mark_mvp_events_as_supported(string eventType)
    {
        var category = StripeWebhookEventClassifier.Classify(eventType);

        Assert.Equal(StripeWebhookEventCategory.Supported, category);
    }

    [Theory]
    [InlineData("charge.dispute.created")]
    [InlineData("checkout.session.completed")]
    [InlineData("invoice.payment_succeeded")]
    public void Classify_should_mark_known_non_mvp_events_as_known_unsupported(string eventType)
    {
        var category = StripeWebhookEventClassifier.Classify(eventType);

        Assert.Equal(StripeWebhookEventCategory.KnownUnsupported, category);
    }

    [Fact]
    public void Classify_should_mark_unrecognized_events_as_unknown()
    {
        var category = StripeWebhookEventClassifier.Classify("treasury.received_credit.created");

        Assert.Equal(StripeWebhookEventCategory.Unknown, category);
    }

    [Fact]
    public void CreateStripe_should_build_pending_inbox_message_for_supported_event()
    {
        var paymentId = PaymentId.New();

        var message = PaymentInboxMessage.CreateStripe(
            "evt_123",
            "payment_intent.succeeded",
            Payload,
            Now,
            "correlation-1",
            "pi_123",
            paymentId);

        Assert.Equal(PaymentProvider.Stripe, message.Provider);
        Assert.Equal("evt_123", message.ProviderEventId);
        Assert.Equal("payment_intent.succeeded", message.EventType);
        Assert.Equal(PaymentInboxStatus.Pending, message.Status);
        Assert.Equal(StripeWebhookEventCategory.Supported, message.EventCategory);
        Assert.Equal("correlation-1", message.CorrelationId);
        Assert.Equal("pi_123", message.ProviderPaymentId);
        Assert.Equal(paymentId, message.PaymentId);
        Assert.Equal(64, message.PayloadSha256.Length);
        Assert.Equal(Now, message.ReceivedAt);
        Assert.Equal(0, message.AttemptCount);
        Assert.Null(message.ProcessedAt);
    }

    [Theory]
    [InlineData("charge.dispute.created", StripeWebhookEventCategory.KnownUnsupported)]
    [InlineData("unknown.event", StripeWebhookEventCategory.Unknown)]
    public void CreateStripe_should_build_ignored_inbox_message_for_non_mvp_events(
        string eventType,
        StripeWebhookEventCategory expectedCategory)
    {
        var message = PaymentInboxMessage.CreateStripe(
            "evt_123",
            eventType,
            Payload,
            Now,
            null,
            null,
            null);

        Assert.Equal(PaymentInboxStatus.Ignored, message.Status);
        Assert.Equal(expectedCategory, message.EventCategory);
    }

    [Fact]
    public async Task Handler_should_persist_message_and_return_inbox_outcome()
    {
        var repository = new Mock<IPaymentInboxRepository>(MockBehavior.Strict);
        repository
            .Setup(x => x.StoreAsync(
                It.Is<PaymentInboxMessage>(message =>
                    message.ProviderEventId == "evt_123"
                    && message.EventType == "payment_intent.processing"
                    && message.Status == PaymentInboxStatus.Pending),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentInboxStoreResult.Inserted);
        var handler = new ReceiveStripeWebhookCommandHandler(repository.Object, new FixedClock(Now));

        var result = await handler.Handle(new ReceiveStripeWebhookCommand(
            "evt_123",
            "payment_intent.processing",
            Payload,
            "correlation-1",
            "pi_123",
            null), CancellationToken.None);

        Assert.Equal(PaymentInboxStoreResult.Inserted, result.StoreResult);
        Assert.Equal(PaymentInboxStatus.Pending, result.InboxStatus);
        Assert.Equal(StripeWebhookEventCategory.Supported, result.EventCategory);
        repository.VerifyAll();
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
