using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Moq;

using PaymentService.Application.Abstractions.Gateway;
using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Abstractions.Time;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Payments.Commands;
using PaymentService.Domain.Payments;

namespace PaymentService.UnitTests.Application.Payments;

public sealed class RequestRefundCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid LedgerEntryId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Handle_should_return_replay_when_refund_response_is_complete()
    {
        var payment = CompletedPayment();
        var refund = RequestedRefund(payment);
        payment.RegisterRefundProviderCreated(Now, refund.RefundId, "re_complete", "pending");
        var response = ToResult(payment, refund);
        var idempotency = CreateIdempotency(response, ExpectedHash(payment.PaymentId.Value));
        var gateway = new Mock<IPaymentGateway>();
        var handler = CreateHandler(payment, idempotency, gateway);

        var result = await handler.Handle(Command(payment.PaymentId.Value), CancellationToken.None);

        Assert.True(result.IdempotentReplay);
        Assert.Equal(refund.RefundId.Value, result.RefundId);
        Assert.Equal("re_complete", result.ProviderRefundId);
        gateway.Verify(x => x.CreateRefundAsync(It.IsAny<CreateExternalRefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_should_resume_incomplete_idempotent_refund_with_same_refund_and_external_key()
    {
        var payment = CompletedPayment();
        var refund = RequestedRefund(payment);
        var response = ToResult(payment, refund);
        var idempotency = CreateIdempotency(response, ExpectedHash(payment.PaymentId.Value));
        var gateway = CreateGateway("re_resumed", "pending");
        var handler = CreateHandler(payment, idempotency, gateway);

        var result = await handler.Handle(Command(payment.PaymentId.Value), CancellationToken.None);

        Assert.True(result.IdempotentReplay);
        Assert.Equal(refund.RefundId.Value, result.RefundId);
        Assert.Equal("ProviderPending", result.Status);
        Assert.Equal("re_resumed", result.ProviderRefundId);
        Assert.Single(payment.Refunds);
        gateway.Verify(x => x.CreateRefundAsync(
            It.Is<CreateExternalRefundRequest>(request =>
                request.RefundId == refund.RefundId.Value &&
                request.IdempotencyKey == RequestRefundCommandHandler.BuildExternalRefundIdempotencyKey(payment.PaymentId.Value, refund.RefundId.Value)),
            It.IsAny<CancellationToken>()), Times.Once);
        idempotency.Verify(x => x.UpdateRefundResponseAsync(
            "merchant-001",
            "refund-key",
            It.Is<RequestRefundResult>(stored => !stored.IdempotentReplay && stored.ProviderRefundId == "re_resumed"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_should_keep_incomplete_refund_retryable_when_provider_fails_transiently()
    {
        var payment = CompletedPayment();
        var idempotency = new Mock<IPaymentIdempotencyService>();
        var gateway = new Mock<IPaymentGateway>();
        gateway.Setup(x => x.CreateRefundAsync(It.IsAny<CreateExternalRefundRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PaymentGatewayException(PaymentGatewayErrorCategory.Transient, "timeout", "timeout"));
        var handler = CreateHandler(payment, idempotency, gateway);

        var exception = await Assert.ThrowsAsync<ExternalPaymentProviderException>(
            () => handler.Handle(Command(payment.PaymentId.Value), CancellationToken.None));

        var refund = Assert.Single(payment.Refunds);
        Assert.Equal(PaymentGatewayErrorCategory.Transient, exception.Category);
        Assert.Equal(RefundStatus.Requested, refund.Status);
        Assert.Null(refund.ProviderRefundId);
        idempotency.Verify(x => x.AddRefundAsync(
            "merchant-001",
            "refund-key",
            ExpectedHash(payment.PaymentId.Value),
            It.Is<RequestRefundResult>(response => response.Status == "Requested"),
            Now.AddDays(7),
            It.IsAny<CancellationToken>()), Times.Once);
        idempotency.Verify(x => x.UpdateRefundResponseAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<RequestRefundResult>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_should_mark_refund_provider_failed_when_provider_returns_failed()
    {
        var payment = CompletedPayment();
        var idempotency = new Mock<IPaymentIdempotencyService>();
        var gateway = CreateGateway("re_failed", "failed");
        var handler = CreateHandler(payment, idempotency, gateway);

        var result = await handler.Handle(Command(payment.PaymentId.Value), CancellationToken.None);

        var refund = Assert.Single(payment.Refunds);
        Assert.Equal("ProviderFailed", result.Status);
        Assert.Equal(RefundStatus.ProviderFailed, refund.Status);
        Assert.Equal("re_failed", result.ProviderRefundId);
        idempotency.Verify(x => x.UpdateRefundResponseAsync(
            "merchant-001",
            "refund-key",
            It.Is<RequestRefundResult>(response => response.Status == "ProviderFailed"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_should_reject_same_idempotency_key_with_different_payload()
    {
        var payment = CompletedPayment();
        var refund = RequestedRefund(payment);
        var idempotency = CreateIdempotency(ToResult(payment, refund), "different");
        var gateway = new Mock<IPaymentGateway>();
        var handler = CreateHandler(payment, idempotency, gateway);

        await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(Command(payment.PaymentId.Value), CancellationToken.None));

        gateway.Verify(x => x.CreateRefundAsync(It.IsAny<CreateExternalRefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_should_not_regress_refund_when_webhook_advanced_before_provider_response()
    {
        var paymentId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        var snapshotPayment = CompletedPayment(paymentId);
        var snapshotRefund = snapshotPayment.RequestRefund(
            new RefundId(refundId),
            snapshotPayment.Amount,
            "requested_by_customer",
            "refund-ext",
            "correlation-1",
            Now);
        var advancedPayment = CompletedPayment(paymentId);
        var advancedRefund = advancedPayment.RequestRefund(
            new RefundId(refundId),
            advancedPayment.Amount,
            "requested_by_customer",
            "refund-ext",
            "correlation-1",
            Now);
        advancedPayment.MarkRefundProviderSucceeded(Now.AddSeconds(1), advancedRefund.RefundId, "re_webhook", "succeeded");
        var response = ToResult(snapshotPayment, snapshotRefund);
        var idempotency = CreateIdempotency(response, ExpectedHash(paymentId));
        var repository = new Mock<IPaymentRepository>();
        repository.SetupSequence(x => x.GetByIdAsync(new PaymentId(paymentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshotPayment)
            .ReturnsAsync(snapshotPayment);
        repository.SetupSequence(x => x.GetByIdForUpdateAsync(new PaymentId(paymentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(advancedPayment);
        var gateway = CreateGateway("re_late_pending", "pending");
        var handler = CreateHandler(repository, idempotency, gateway);

        var result = await handler.Handle(Command(paymentId), CancellationToken.None);

        Assert.True(result.IdempotentReplay);
        Assert.Equal("LedgerReversalPending", result.Status);
        Assert.Equal("re_webhook", result.ProviderRefundId);
        Assert.Equal(RefundStatus.LedgerReversalPending, advancedRefund.Status);
        Assert.Single(advancedPayment.Refunds);
        gateway.Verify(x => x.CreateRefundAsync(It.IsAny<CreateExternalRefundRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static RequestRefundCommandHandler CreateHandler(
        Payment payment,
        Mock<IPaymentIdempotencyService>? idempotency = null,
        Mock<IPaymentGateway>? gateway = null)
    {
        var repository = new Mock<IPaymentRepository>();
        repository.Setup(x => x.GetByIdAsync(payment.PaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        repository.Setup(x => x.GetByIdForUpdateAsync(payment.PaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        return CreateHandler(repository, idempotency, gateway);
    }

    private static RequestRefundCommandHandler CreateHandler(
        Mock<IPaymentRepository> repository,
        Mock<IPaymentIdempotencyService>? idempotency = null,
        Mock<IPaymentGateway>? gateway = null)
        => new(
            repository.Object,
            (idempotency ?? new Mock<IPaymentIdempotencyService>()).Object,
            (gateway ?? CreateGateway("re_test", "pending")).Object,
            CreateUnitOfWork().Object,
            new FixedClock(Now));

    private static Mock<IPaymentIdempotencyService> CreateIdempotency(RequestRefundResult response, string requestHash)
    {
        var idempotency = new Mock<IPaymentIdempotencyService>();
        idempotency.Setup(x => x.GetRefundAsync("merchant-001", "refund-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentRefundIdempotencyEntry(requestHash, response));
        return idempotency;
    }

    private static Mock<IPaymentGateway> CreateGateway(string providerRefundId, string providerStatus)
    {
        var gateway = new Mock<IPaymentGateway>();
        gateway.Setup(x => x.CreateRefundAsync(It.IsAny<CreateExternalRefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateExternalRefundRequest request, CancellationToken _) => new CreateExternalRefundResult(
                "Stripe",
                providerRefundId,
                request.ProviderPaymentId,
                providerStatus,
                request.Amount,
                request.Currency,
                Now,
                providerStatus));
        return gateway;
    }

    private static Payment CompletedPayment(Guid? paymentId = null)
    {
        var payment = new Payment(
            new PaymentId(paymentId ?? Guid.NewGuid()),
            new MerchantId("merchant-001"),
            new Money(100m, Currency.Brl),
            PaymentProvider.Stripe,
            Now,
            "Pagamento original",
            new ExternalReference("order-123"));
        payment.MarkSucceeded(Now, new ExternalPaymentReference("pi_original"), "succeeded", "correlation-1");
        payment.MarkCompleted(Now, new LedgerEntryReference(LedgerEntryId));
        return payment;
    }

    private static PaymentRefund RequestedRefund(Payment payment)
        => payment.RequestRefund(
            new RefundId(Guid.NewGuid()),
            payment.Amount,
            "requested_by_customer",
            "refund-ext",
            "correlation-1",
            Now);

    private static RequestRefundResult ToResult(Payment payment, PaymentRefund refund)
        => new(
            payment.PaymentId.Value,
            refund.RefundId.Value,
            refund.Status.ToString(),
            refund.Amount.Amount,
            refund.Amount.Currency.Code,
            refund.Reason,
            refund.ExternalReference,
            refund.ProviderRefundId,
            refund.ProviderStatus,
            refund.LedgerReversalId,
            refund.CreatedAt,
            refund.UpdatedAt,
            false);

    private static RequestRefundCommand Command(Guid paymentId)
        => new(
            paymentId,
            "refund-key",
            null,
            " requested_by_customer ",
            "refund-ext",
            "correlation-1",
            ["merchant-001"]);

    private static Mock<IUnitOfWork> CreateUnitOfWork()
    {
        var transaction = new Mock<IAppTransaction>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);
        return unitOfWork;
    }

    private static string ExpectedHash(Guid paymentId)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            PaymentId = paymentId,
            Amount = 100m,
            Reason = "requested_by_customer",
            ExternalReference = "refund-ext"
        }, JsonOptions);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
