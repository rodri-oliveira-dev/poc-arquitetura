using PaymentService.Application.Abstractions.Ledger;
using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Abstractions.Time;
using PaymentService.Application.Payments.Ledger;
using PaymentService.Domain.Payments;

namespace PaymentService.UnitTests.Application.Ledger;

public sealed class PaymentLedgerProcessorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 9, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Idempotency_key_should_be_stable_and_operation_specific()
    {
        var paymentId = new PaymentId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

        var first = PaymentLedgerIdempotencyKeyFactory.CreateForCredit(paymentId);
        var second = PaymentLedgerIdempotencyKeyFactory.CreateForCredit(paymentId);
        var other = PaymentLedgerIdempotencyKeyFactory.CreateForCredit(new PaymentId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")));

        Assert.Equal(first, second);
        Assert.NotEqual(Guid.Empty, first);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public async Task Processor_should_create_credit_and_complete_payment()
    {
        var payment = CreateSucceededPayment();
        var ledgerEntryId = new LedgerEntryReference(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var gateway = new FakeLedgerGateway(_ => LedgerEntryCreationResult.Success(ledgerEntryId));
        var processor = CreateProcessor(payment, gateway);

        var result = await processor.ProcessBatchAsync(10, "worker-1", TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.Equal(1, result.Claimed);
        Assert.Equal(1, result.Completed);
        Assert.Equal(PaymentStatus.Completed, payment.Status);
        Assert.Equal(LedgerIntegrationStatus.Completed, payment.LedgerIntegrationStatus);
        Assert.Equal(ledgerEntryId, payment.LedgerEntryReference);
        Assert.Single(gateway.Requests);
        Assert.Equal("payment:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", gateway.Requests.Single().ExternalReference);
        Assert.Equal("correlation-1", gateway.Requests.Single().CorrelationId);
    }

    [Fact]
    public async Task Processor_should_schedule_retry_after_unknown_timeout_and_reuse_same_key()
    {
        var payment = CreateSucceededPayment();
        var gateway = new FakeLedgerGateway(_ => LedgerEntryCreationResult.UnknownResult("timeout"));
        var processor = CreateProcessor(payment, gateway);

        var result = await processor.ProcessBatchAsync(10, "worker-1", TimeSpan.FromMinutes(1), CancellationToken.None);
        var firstKey = gateway.Requests.Single().IdempotencyKey;

        Assert.Equal(1, result.RetryScheduled);
        Assert.Equal(PaymentStatus.LedgerPending, payment.Status);
        Assert.Equal(LedgerIntegrationStatus.RetryScheduled, payment.LedgerIntegrationStatus);
        Assert.NotNull(payment.LedgerNextRetryAt);
        Assert.Null(payment.LedgerEntryReference);

        payment.ClaimLedgerIntegration(payment.LedgerNextRetryAt.Value, "worker-2", payment.LedgerNextRetryAt.Value.AddMinutes(1));
        var retryRequest = BuildRequestForRetry(payment);

        Assert.Equal(firstKey, retryRequest.IdempotencyKey);
    }

    [Fact]
    public async Task Processor_should_mark_definitive_failure_without_retry()
    {
        var payment = CreateSucceededPayment();
        var gateway = new FakeLedgerGateway(_ => LedgerEntryCreationResult.Definitive(
            LedgerEntryFailureCategory.IdempotencyConflict,
            "idempotency conflict"));
        var processor = CreateProcessor(payment, gateway);

        var result = await processor.ProcessBatchAsync(10, "worker-1", TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.Equal(1, result.FailedDefinitive);
        Assert.Equal(PaymentStatus.LedgerPending, payment.Status);
        Assert.Equal(LedgerIntegrationStatus.FailedDefinitive, payment.LedgerIntegrationStatus);
        Assert.Contains("idempotency conflict", payment.LedgerLastError);
    }

    private static PaymentLedgerProcessor CreateProcessor(Payment payment, FakeLedgerGateway gateway)
        => new(
            new FakePaymentRepository(payment),
            gateway,
            new FakeUnitOfWork(),
            new FixedClock(Now),
            new PaymentLedgerProcessingOptions
            {
                MaxRetryCount = 5,
                BaseRetryDelay = TimeSpan.FromSeconds(5),
                MaxRetryDelay = TimeSpan.FromMinutes(5)
            });

    private static Payment CreateSucceededPayment()
    {
        var payment = new Payment(
            new PaymentId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            new MerchantId("merchant-001"),
            new Money(100m, Currency.Brl),
            PaymentProvider.Stripe,
            Now.AddMinutes(-10),
            "Pagamento",
            new ExternalReference("order-123"));
        payment.MarkSucceeded(Now.AddMinutes(-5), new ExternalPaymentReference("pi_123"), "succeeded", "correlation-1");
        return payment;
    }

    private static LedgerCreditRequest BuildRequestForRetry(Payment payment)
        => new(
            payment.PaymentId,
            payment.MerchantId,
            payment.Amount,
            "Payment captured",
            $"payment:{payment.PaymentId.Value}",
            PaymentLedgerIdempotencyKeyFactory.CreateForCredit(payment.PaymentId),
            payment.LedgerCorrelationId);

    private sealed class FakeLedgerGateway(Func<LedgerCreditRequest, LedgerEntryCreationResult> response) : ILedgerEntryGateway
    {
        private readonly Func<LedgerCreditRequest, LedgerEntryCreationResult> _response = response;

        public List<LedgerCreditRequest> Requests { get; } = [];

        public Task<LedgerEntryCreationResult> CreateCreditAsync(LedgerCreditRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_response(request));
        }
    }

    private sealed class FakePaymentRepository(Payment payment) : IPaymentRepository
    {
        private readonly Payment _payment = payment;

        public Task<Payment?> GetByIdAsync(PaymentId paymentId, CancellationToken cancellationToken)
            => Task.FromResult(_payment.PaymentId == paymentId ? _payment : null);

        public Task<Payment?> GetByIdForUpdateAsync(PaymentId paymentId, CancellationToken cancellationToken)
            => GetByIdAsync(paymentId, cancellationToken);

        public Task<Payment?> GetByProviderReferenceForUpdateAsync(
            PaymentProvider provider,
            ExternalPaymentReference externalPaymentReference,
            CancellationToken cancellationToken)
            => Task.FromResult<Payment?>(null);

        public Task<IReadOnlyList<Payment>> ClaimLedgerIntegrationAsync(
            int batchSize,
            DateTimeOffset now,
            string lockOwner,
            TimeSpan leaseTimeout,
            CancellationToken cancellationToken)
        {
            var claimed = _payment.ClaimLedgerIntegration(now, lockOwner, now.Add(leaseTimeout))
                ? new List<Payment> { _payment }
                : [];

            return Task.FromResult<IReadOnlyList<Payment>>(claimed);
        }

        public Task AddAsync(Payment payment, CancellationToken cancellationToken)
            => Task.CompletedTask;
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
