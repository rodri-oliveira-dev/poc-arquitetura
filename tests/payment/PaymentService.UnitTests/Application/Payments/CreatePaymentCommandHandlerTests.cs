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

public sealed class CreatePaymentCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset Now = new(2026, 7, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_should_create_payment_intent_and_persist_provider_reference()
    {
        var repository = new Mock<IPaymentRepository>();
        var idempotency = new Mock<IPaymentIdempotencyService>();
        var gateway = CreateGateway();
        var unitOfWork = new Mock<IUnitOfWork>();
        var transaction = new Mock<IAppTransaction>();
        unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);
        var handler = CreateHandler(repository, idempotency, gateway, unitOfWork);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal("RequiresAction", result.Status);
        Assert.Equal("merchant-001", result.MerchantId);
        Assert.Equal(100m, result.Amount);
        Assert.Equal("BRL", result.Currency);
        Assert.Equal("Stripe", result.Provider);
        Assert.Equal("pi_test_123", result.ExternalPaymentReference);
        Assert.Equal("requires_payment_method", result.ProviderStatus);
        Assert.Equal("payment-client-secret-placeholder", result.ClientSecret);
        Assert.False(result.IdempotentReplay);
        repository.Verify(x => x.AddAsync(
            It.Is<Payment>(payment => payment.MerchantId.Value == "merchant-001"),
            It.IsAny<CancellationToken>()), Times.Once);
        gateway.Verify(x => x.CreatePaymentIntentAsync(
            It.Is<CreateExternalPaymentRequest>(request =>
                request.MerchantId == "merchant-001"
                && request.Currency == "BRL"
                && request.Description == "desc"
                && request.ExternalReference == "order-123"
                && request.IdempotencyKey == CreatePaymentCommandHandler.BuildExternalIdempotencyKey(request.PaymentId)),
            It.IsAny<CancellationToken>()), Times.Once);
        idempotency.Verify(x => x.AddAsync(
            "merchant-001",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<CreatePaymentResult>(response => response.PaymentId == result.PaymentId),
            Now.AddDays(7),
            It.IsAny<CancellationToken>()), Times.Once);
        idempotency.Verify(x => x.UpdateResponseAsync(
            "merchant-001",
            It.IsAny<string>(),
            It.Is<CreatePaymentResult>(response =>
                response.PaymentId == result.PaymentId
                && response.ExternalPaymentReference == "pi_test_123"
                && response.ClientSecret == null),
            It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_should_return_replay_when_idempotency_payload_matches()
    {
        var expected = new CreatePaymentResult(
            Guid.NewGuid(),
            "Pending",
            "merchant-001",
            100m,
            "BRL",
            "Stripe",
            "desc",
            "order-123",
            "pi_test_123",
            "requires_payment_method",
            null,
            null,
            Now,
            Now,
            false);
        var idempotency = new Mock<IPaymentIdempotencyService>();
        idempotency.Setup(x => x.GetAsync("merchant-001", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIdempotencyEntry(ExpectedHash(), expected));
        var handler = CreateHandler(idempotencyService: idempotency);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IdempotentReplay);
        Assert.Equal(expected.PaymentId, result.PaymentId);
        Assert.Null(result.ClientSecret);
    }

    [Fact]
    public async Task Handle_should_resume_external_creation_for_incomplete_idempotent_record()
    {
        var paymentId = Guid.NewGuid();
        var payment = new Payment(
            new PaymentId(paymentId),
            new MerchantId("merchant-001"),
            new Money(100m, Currency.Brl),
            PaymentProvider.Stripe,
            Now,
            "desc",
            new ExternalReference("order-123"));
        var repository = new Mock<IPaymentRepository>();
        repository.Setup(x => x.GetByIdAsync(new PaymentId(paymentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var idempotency = new Mock<IPaymentIdempotencyService>();
        idempotency.Setup(x => x.GetAsync("merchant-001", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIdempotencyEntry(ExpectedHash(), ToPendingResult(payment)));
        var gateway = CreateGateway();
        var handler = CreateHandler(repository, idempotency, gateway);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IdempotentReplay);
        Assert.Equal(paymentId, result.PaymentId);
        Assert.Equal("pi_test_123", result.ExternalPaymentReference);
        gateway.Verify(x => x.CreatePaymentIntentAsync(
            It.Is<CreateExternalPaymentRequest>(request =>
                request.PaymentId == paymentId
                && request.IdempotencyKey == CreatePaymentCommandHandler.BuildExternalIdempotencyKey(paymentId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_should_reject_idempotency_key_with_different_payload()
    {
        var idempotency = new Mock<IPaymentIdempotencyService>();
        idempotency.Setup(x => x.GetAsync("merchant-001", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIdempotencyEntry("different", new CreatePaymentResult(
                Guid.NewGuid(),
                "Pending",
                "merchant-001",
                100m,
                "BRL",
                "Stripe",
                null,
                null,
                null,
                null,
                null,
                null,
                Now,
                Now,
                false)));
        var handler = CreateHandler(idempotencyService: idempotency);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(Command(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_should_surface_unknown_result_timeout_without_generating_new_external_key()
    {
        var gateway = new Mock<IPaymentGateway>();
        gateway.Setup(x => x.CreatePaymentIntentAsync(It.IsAny<CreateExternalPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PaymentGatewayException(
                PaymentGatewayErrorCategory.UnknownResult,
                "timeout",
                "timeout"));
        var handler = CreateHandler(gateway: gateway);

        var exception = await Assert.ThrowsAsync<ExternalPaymentProviderException>(
            () => handler.Handle(Command(), CancellationToken.None));

        Assert.Equal(PaymentGatewayErrorCategory.UnknownResult, exception.Category);
        gateway.Verify(x => x.CreatePaymentIntentAsync(
            It.Is<CreateExternalPaymentRequest>(request =>
                request.IdempotencyKey == CreatePaymentCommandHandler.BuildExternalIdempotencyKey(request.PaymentId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void BuildExternalIdempotencyKey_should_be_deterministic_for_same_payment()
    {
        var paymentId = Guid.NewGuid();

        var first = CreatePaymentCommandHandler.BuildExternalIdempotencyKey(paymentId);
        var second = CreatePaymentCommandHandler.BuildExternalIdempotencyKey(paymentId);

        Assert.Equal(first, second);
        Assert.Contains(paymentId.ToString("N"), first, StringComparison.Ordinal);
        Assert.Contains("stripe:create-payment-intent", first, StringComparison.Ordinal);
    }

    private static CreatePaymentCommandHandler CreateHandler(
        Mock<IPaymentRepository>? repository = null,
        Mock<IPaymentIdempotencyService>? idempotencyService = null,
        Mock<IPaymentGateway>? gateway = null,
        Mock<IUnitOfWork>? unitOfWork = null)
        => new(
            (repository ?? new Mock<IPaymentRepository>()).Object,
            (idempotencyService ?? new Mock<IPaymentIdempotencyService>()).Object,
            (gateway ?? CreateGateway()).Object,
            (unitOfWork ?? CreateUnitOfWork()).Object,
            new FixedClock(Now));

    private static Mock<IPaymentGateway> CreateGateway()
    {
        var gateway = new Mock<IPaymentGateway>();
        gateway.Setup(x => x.CreatePaymentIntentAsync(It.IsAny<CreateExternalPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateExternalPaymentResult(
                "Stripe",
                "pi_test_123",
                "requires_payment_method",
                "payment-client-secret-placeholder",
                true,
                "requires_payment_method"));
        return gateway;
    }

    private static CreatePaymentResult ToPendingResult(Payment payment)
        => new(
            payment.PaymentId.Value,
            payment.Status.ToString(),
            payment.MerchantId.Value,
            payment.Amount.Amount,
            payment.Amount.Currency.Code,
            payment.Provider.ToString(),
            payment.Description,
            payment.ExternalReference?.Value,
            payment.ExternalPaymentReference?.Value,
            payment.ProviderStatus,
            null,
            payment.LedgerEntryReference?.Value,
            payment.CreatedAt,
            payment.UpdatedAt,
            false);

    private static Mock<IUnitOfWork> CreateUnitOfWork()
    {
        var transaction = new Mock<IAppTransaction>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);
        return unitOfWork;
    }

    private static CreatePaymentCommand Command()
        => new(Guid.NewGuid().ToString(), " merchant-001 ", 100m, "brl", "desc", "order-123", "correlation-1");

    private static string ExpectedHash()
    {
        var canonical = JsonSerializer.Serialize(new
        {
            MerchantId = "merchant-001",
            Amount = 100m,
            Currency = "BRL",
            Description = "desc",
            ExternalReference = "order-123"
        }, JsonOptions);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
