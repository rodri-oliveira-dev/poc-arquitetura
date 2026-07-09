using Moq;

using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Abstractions.Time;
using PaymentService.Application.Payments.InboxProcessing;
using PaymentService.Application.Payments.Webhooks;
using PaymentService.Domain.Payments;

namespace PaymentService.UnitTests.Application.InboxProcessing;

public sealed class PaymentInboxProcessingTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 9, 14, 0, 0, TimeSpan.Zero);
    private const string Payload = /*lang=json,strict*/ "{\"id\":\"evt_123\"}";

    [Fact]
    public void Mapper_should_translate_supported_stripe_event()
    {
        var message = CreateMessage("payment_intent.succeeded");
        var mapper = new StripeInboxProviderEventMapper();

        var result = mapper.Map(message);

        Assert.False(result.IsPermanentFailure);
        Assert.NotNull(result.Event);
        Assert.Equal(PaymentProviderEventKind.Succeeded, result.Event.Kind);
        Assert.Equal(new ExternalPaymentReference("pi_123"), result.Event.ProviderPaymentReference);
    }

    [Fact]
    public void Retry_policy_should_apply_exponential_backoff_with_cap()
    {
        Assert.Equal(Now.AddSeconds(5), PaymentInboxRetryPolicy.CalculateNextRetryAt(Now, 1, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5)));
        Assert.Equal(Now.AddSeconds(20), PaymentInboxRetryPolicy.CalculateNextRetryAt(Now, 3, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5)));
        Assert.Equal(Now.AddMinutes(5), PaymentInboxRetryPolicy.CalculateNextRetryAt(Now, 10, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task Handler_should_apply_succeeded_event_and_mark_inbox_processed()
    {
        var payment = CreatePayment();
        payment.MarkProcessing(Now.AddMinutes(-1), new ExternalPaymentReference("pi_123"), "processing");
        var message = CreateClaimedMessage("payment_intent.succeeded");
        var handler = CreateHandler(message, payment);

        var result = await handler.Handle(new ProcessPaymentInboxMessageCommand(message.Id, "worker-1"), CancellationToken.None);

        Assert.Equal(PaymentInboxStatus.Processed, result.Status);
        Assert.Equal("processed", result.Outcome);
        Assert.True(result.PaymentChanged);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(PaymentInboxStatus.Processed, message.Status);
    }

    [Fact]
    public async Task Handler_should_treat_repeated_succeeded_event_as_idempotent()
    {
        var payment = CreatePayment();
        payment.MarkSucceeded(Now.AddMinutes(-1), new ExternalPaymentReference("pi_123"), "succeeded");
        var message = CreateClaimedMessage("payment_intent.succeeded");
        var handler = CreateHandler(message, payment);

        var result = await handler.Handle(new ProcessPaymentInboxMessageCommand(message.Id, "worker-1"), CancellationToken.None);

        Assert.Equal("idempotent", result.Outcome);
        Assert.False(result.PaymentChanged);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(PaymentInboxStatus.Processed, message.Status);
    }

    [Fact]
    public async Task Handler_should_ignore_regressive_processing_event_after_succeeded()
    {
        var payment = CreatePayment();
        payment.MarkSucceeded(Now.AddMinutes(-1), new ExternalPaymentReference("pi_123"), "succeeded");
        var message = CreateClaimedMessage("payment_intent.processing");
        var handler = CreateHandler(message, payment);

        var result = await handler.Handle(new ProcessPaymentInboxMessageCommand(message.Id, "worker-1"), CancellationToken.None);

        Assert.Equal("regressive_ignored", result.Outcome);
        Assert.False(result.PaymentChanged);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(PaymentInboxStatus.Processed, message.Status);
    }

    [Fact]
    public async Task Handler_should_schedule_retry_when_payment_is_missing()
    {
        var message = CreateClaimedMessage("payment_intent.succeeded");
        var handler = CreateHandler(message, payment: null);

        var result = await handler.Handle(new ProcessPaymentInboxMessageCommand(message.Id, "worker-1"), CancellationToken.None);

        Assert.Equal("retry_scheduled", result.Outcome);
        Assert.Equal(PaymentInboxStatus.RetryScheduled, result.Status);
        Assert.NotNull(message.NextRetryAt);
        Assert.Equal(1, message.AttemptCount);
    }

    [Fact]
    public async Task Handler_should_deadletter_missing_payment_after_max_attempts()
    {
        var message = CreateClaimedMessage("payment_intent.succeeded");
        message.ScheduleRetry(Now.AddMinutes(-10), Now.AddMinutes(-5), "Payment not found.");
        message.MarkProcessing("worker-1", Now, Now.AddMinutes(1));
        var handler = CreateHandler(
            message,
            payment: null,
            new PaymentInboxProcessingOptions
            {
                MaxRetryCount = 2,
                BaseRetryDelay = TimeSpan.FromSeconds(5),
                MaxRetryDelay = TimeSpan.FromMinutes(5)
            });

        var result = await handler.Handle(new ProcessPaymentInboxMessageCommand(message.Id, "worker-1"), CancellationToken.None);

        Assert.Equal("dead_letter", result.Outcome);
        Assert.Equal(PaymentInboxStatus.DeadLetter, result.Status);
        Assert.Contains("maximum retries", message.LastError);
    }

    private static ProcessPaymentInboxMessageCommandHandler CreateHandler(
        PaymentInboxMessage message,
        Payment? payment,
        PaymentInboxProcessingOptions? options = null)
    {
        var inboxRepository = new Mock<IPaymentInboxRepository>(MockBehavior.Strict);
        inboxRepository
            .Setup(x => x.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var paymentRepository = new Mock<IPaymentRepository>(MockBehavior.Strict);
        if (payment is null)
        {
            paymentRepository
                .Setup(x => x.GetByIdForUpdateAsync(It.IsAny<PaymentId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Payment?)null);
        }
        else
        {
            paymentRepository
                .Setup(x => x.GetByIdForUpdateAsync(payment.PaymentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(payment);
        }

        return new ProcessPaymentInboxMessageCommandHandler(
            inboxRepository.Object,
            paymentRepository.Object,
            new FakeUnitOfWork(),
            new StripeInboxProviderEventMapper(),
            new FixedClock(Now),
            options ?? new PaymentInboxProcessingOptions());
    }

    private static Payment CreatePayment()
        => new(
            new PaymentId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            new MerchantId("merchant-001"),
            new Money(100m, Currency.Brl),
            PaymentProvider.Stripe,
            Now.AddMinutes(-5),
            "Pagamento",
            new ExternalReference("order-123"));

    private static PaymentInboxMessage CreateMessage(string eventType)
        => PaymentInboxMessage.CreateStripe(
            $"evt_{eventType.Replace(".", "_", StringComparison.Ordinal)}",
            eventType,
            Payload,
            Now,
            "correlation-1",
            "pi_123",
            new PaymentId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));

    private static PaymentInboxMessage CreateClaimedMessage(string eventType)
    {
        var message = CreateMessage(eventType);
        message.MarkProcessing("worker-1", Now, Now.AddMinutes(1));
        return message;
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IAppTransaction>(new FakeTransaction());

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
            => Task.FromResult(1);
    }

    private sealed class FakeTransaction : IAppTransaction
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
