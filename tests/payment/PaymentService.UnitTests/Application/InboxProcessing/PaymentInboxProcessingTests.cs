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

    [Theory]
    [InlineData("payment_intent.processing", PaymentProviderEventKind.Processing, "processing")]
    [InlineData("payment_intent.payment_failed", PaymentProviderEventKind.Failed, "payment_failed")]
    [InlineData("payment_intent.canceled", PaymentProviderEventKind.Cancelled, "canceled")]
    public void Mapper_should_translate_payment_intent_events(
        string eventType,
        PaymentProviderEventKind expectedKind,
        string expectedStatus)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(expectedStatus);

        var message = CreateMessage(eventType);
        var mapper = new StripeInboxProviderEventMapper();

        var result = mapper.Map(message);

        Assert.False(result.IsPermanentFailure);
        Assert.NotNull(result.Event);
        Assert.Equal(expectedKind, result.Event.Kind);
        Assert.Equal(expectedStatus, result.Event.ProviderStatus);
        Assert.Equal(new ExternalPaymentReference("pi_123"), result.Event.ProviderPaymentReference);
    }

    [Theory]
    [InlineData("refund.created", "pending", PaymentProviderEventKind.RefundCreated)]
    [InlineData("refund.created", "succeeded", PaymentProviderEventKind.RefundSucceeded)]
    [InlineData("refund.updated", "pending", PaymentProviderEventKind.RefundCreated)]
    [InlineData("refund.updated", "succeeded", PaymentProviderEventKind.RefundSucceeded)]
    [InlineData("refund.updated", "failed", PaymentProviderEventKind.RefundFailed)]
    [InlineData("refund.failed", "failed", PaymentProviderEventKind.RefundFailed)]
    public void Mapper_should_translate_refund_events(
        string eventType,
        string providerStatus,
        PaymentProviderEventKind expectedKind)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(providerStatus);

        var refundId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var message = CreateRefundMessage(eventType, providerStatus, refundId);
        var mapper = new StripeInboxProviderEventMapper();

        var result = mapper.Map(message);

        Assert.False(result.IsPermanentFailure);
        Assert.NotNull(result.Event);
        Assert.Equal(expectedKind, result.Event.Kind);
        Assert.Equal(new RefundId(refundId), result.Event.RefundId);
        Assert.Equal("re_123", result.Event.ProviderRefundId);
        Assert.Equal(new ExternalPaymentReference("pi_123"), result.Event.ProviderPaymentReference);
        Assert.Equal(100m, result.Event.RefundAmount);
        Assert.Equal("BRL", result.Event.RefundCurrency);
    }

    [Fact]
    public void Mapper_should_fail_unknown_or_unsupported_event()
    {
        var message = PaymentInboxMessage.CreateStripe(
            "evt_charge_refunded",
            "charge.refunded",
            Payload,
            Now,
            "correlation-1",
            "pi_123",
            new PaymentId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));
        var mapper = new StripeInboxProviderEventMapper();

        var result = mapper.Map(message);

        Assert.True(result.IsPermanentFailure);
        Assert.Contains("Unsupported provider event type", result.Reason);
    }

    [Fact]
    public void Mapper_should_fail_payment_intent_without_provider_payment_id()
    {
        var message = PaymentInboxMessage.CreateStripe(
            "evt_missing_provider_payment",
            "payment_intent.succeeded",
            Payload,
            Now,
            "correlation-1",
            null,
            new PaymentId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));
        var mapper = new StripeInboxProviderEventMapper();

        var result = mapper.Map(message);

        Assert.True(result.IsPermanentFailure);
        Assert.Contains("provider payment identifier missing", result.Reason);
    }

    [Fact]
    public void Mapper_should_fail_refund_payload_without_internal_refund_id()
    {
        var message = CreateRefundMessage("refund.updated", "succeeded", refundId: null);
        var mapper = new StripeInboxProviderEventMapper();

        var result = mapper.Map(message);

        Assert.True(result.IsPermanentFailure);
        Assert.Contains("internal refund identifier missing", result.Reason);
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

    [Theory]
    [InlineData("payment_intent.payment_failed", PaymentStatus.Failed, "payment_failed")]
    [InlineData("payment_intent.canceled", PaymentStatus.Cancelled, "canceled")]
    public async Task Handler_should_apply_terminal_payment_intent_events(
        string eventType,
        PaymentStatus expectedStatus,
        string expectedProviderStatus)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(expectedProviderStatus);

        var payment = CreatePayment();
        var message = CreateClaimedMessage(eventType);
        var handler = CreateHandler(message, payment);

        var result = await handler.Handle(new ProcessPaymentInboxMessageCommand(message.Id, "worker-1"), CancellationToken.None);

        Assert.Equal("processed", result.Outcome);
        Assert.True(result.PaymentChanged);
        Assert.Equal(expectedStatus, payment.Status);
        Assert.Equal(expectedProviderStatus, payment.ProviderStatus);
        Assert.Equal(PaymentInboxStatus.Processed, message.Status);
    }

    [Fact]
    public async Task Handler_should_apply_refund_succeeded_event_and_schedule_ledger_reversal()
    {
        var payment = CreatePayment();
        payment.MarkSucceeded(Now.AddMinutes(-4), new ExternalPaymentReference("pi_123"), "succeeded");
        payment.MarkCompleted(Now.AddMinutes(-3), new LedgerEntryReference(Guid.Parse("11111111-1111-1111-1111-111111111111")));
        var refund = payment.RequestRefund(
            new RefundId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            payment.Amount,
            "requested_by_customer",
            "refund-ext",
            "correlation-1",
            Now.AddMinutes(-2));
        var message = CreateClaimedRefundMessage("refund.updated", "succeeded", refund.RefundId.Value);
        var handler = CreateHandler(message, payment);

        var result = await handler.Handle(new ProcessPaymentInboxMessageCommand(message.Id, "worker-1"), CancellationToken.None);

        Assert.Equal("processed", result.Outcome);
        Assert.True(result.PaymentChanged);
        Assert.Equal(RefundStatus.LedgerReversalPending, refund.Status);
        Assert.Equal(LedgerIntegrationStatus.Pending, refund.LedgerReversalStatus);
        Assert.Equal("re_123", refund.ProviderRefundId);
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

    private static PaymentInboxMessage CreateRefundMessage(string eventType, string status, Guid? refundId)
    {
        var metadata = refundId is null
            ? string.Empty
            : $",\"metadata\":{{\"refund_id\":\"{refundId.Value:D}\"}}";
        var payload = "{\"id\":\"evt_123\",\"data\":{\"object\":{\"id\":\"re_123\",\"payment_intent\":\"pi_123\",\"status\":\"" +
            status +
            "\",\"amount\":10000,\"currency\":\"brl\"" +
            metadata +
            "}}}";

        return PaymentInboxMessage.CreateStripe(
            $"evt_{eventType.Replace(".", "_", StringComparison.Ordinal)}",
            eventType,
            payload,
            Now,
            "correlation-1",
            null,
            new PaymentId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));
    }

    private static PaymentInboxMessage CreateClaimedMessage(string eventType)
    {
        var message = CreateMessage(eventType);
        message.MarkProcessing("worker-1", Now, Now.AddMinutes(1));
        return message;
    }

    private static PaymentInboxMessage CreateClaimedRefundMessage(string eventType, string status, Guid refundId)
    {
        var message = CreateRefundMessage(eventType, status, refundId);
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
