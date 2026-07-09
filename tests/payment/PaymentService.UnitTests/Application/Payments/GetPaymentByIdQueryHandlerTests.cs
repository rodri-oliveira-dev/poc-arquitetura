using Moq;

using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Common.Exceptions;
using PaymentService.Application.Payments.Queries;
using PaymentService.Domain.Payments;

namespace PaymentService.UnitTests.Application.Payments;

public sealed class GetPaymentByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_should_return_existing_payment()
    {
        var payment = CreatePayment("merchant-001");
        var repository = new Mock<IPaymentRepository>();
        repository.Setup(x => x.GetByIdAsync(payment.PaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var handler = new GetPaymentByIdQueryHandler(repository.Object);

        var result = await handler.Handle(
            new GetPaymentByIdQuery(payment.PaymentId.Value, ["merchant-001"]),
            CancellationToken.None);

        Assert.Equal(payment.PaymentId.Value, result.PaymentId);
        Assert.Equal("Pending", result.Status);
        Assert.Equal("merchant-001", result.MerchantId);
    }

    [Fact]
    public async Task Handle_should_throw_not_found_when_payment_does_not_exist()
    {
        var repository = new Mock<IPaymentRepository>();
        var handler = new GetPaymentByIdQueryHandler(repository.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetPaymentByIdQuery(Guid.NewGuid(), ["merchant-001"]),
            CancellationToken.None));
    }

    [Fact]
    public async Task Handle_should_reject_unauthorized_merchant()
    {
        var payment = CreatePayment("merchant-001");
        var repository = new Mock<IPaymentRepository>();
        repository.Setup(x => x.GetByIdAsync(payment.PaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var handler = new GetPaymentByIdQueryHandler(repository.Object);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new GetPaymentByIdQuery(payment.PaymentId.Value, ["other-merchant"]),
            CancellationToken.None));
    }

    private static Payment CreatePayment(string merchantId)
        => new(
            PaymentId.New(),
            new MerchantId(merchantId),
            new Money(100m, Currency.Brl),
            PaymentProvider.Stripe,
            new DateTimeOffset(2026, 7, 9, 10, 0, 0, TimeSpan.Zero));
}
