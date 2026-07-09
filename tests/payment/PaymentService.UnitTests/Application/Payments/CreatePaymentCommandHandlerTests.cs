using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Moq;

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
    public async Task Handle_should_create_pending_payment_and_persist_idempotency()
    {
        var repository = new Mock<IPaymentRepository>();
        var idempotency = new Mock<IPaymentIdempotencyService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var transaction = new Mock<IAppTransaction>();
        unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);
        var handler = CreateHandler(repository, idempotency, unitOfWork);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal("Pending", result.Status);
        Assert.Equal("merchant-001", result.MerchantId);
        Assert.Equal(100m, result.Amount);
        Assert.Equal("BRL", result.Currency);
        Assert.False(result.IdempotentReplay);
        repository.Verify(x => x.AddAsync(
            It.Is<Payment>(payment => payment.Status == PaymentStatus.Pending && payment.MerchantId.Value == "merchant-001"),
            It.IsAny<CancellationToken>()), Times.Once);
        idempotency.Verify(x => x.AddAsync(
            "merchant-001",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<CreatePaymentResult>(response => response.PaymentId == result.PaymentId),
            Now.AddDays(7),
            It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
            "desc",
            "order-123",
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

    private static CreatePaymentCommandHandler CreateHandler(
        Mock<IPaymentRepository>? repository = null,
        Mock<IPaymentIdempotencyService>? idempotencyService = null,
        Mock<IUnitOfWork>? unitOfWork = null)
        => new(
            (repository ?? new Mock<IPaymentRepository>()).Object,
            (idempotencyService ?? new Mock<IPaymentIdempotencyService>()).Object,
            (unitOfWork ?? CreateUnitOfWork()).Object,
            new FixedClock(Now));

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
