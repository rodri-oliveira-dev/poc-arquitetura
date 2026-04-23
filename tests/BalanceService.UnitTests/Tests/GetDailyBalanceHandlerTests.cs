using BalanceService.Application.Balances.Queries;
using BalanceService.Application.Balances.Queries.Models;
using BalanceService.Application.Balances.Services;
using FluentAssertions;
using Moq;

namespace BalanceService.UnitTests.Tests;

public sealed class GetDailyBalanceHandlerTests
{
    [Fact]
    public async Task Handle_should_delegate_to_daily_balance_service()
    {
        var service = new Mock<IDailyBalanceService>(MockBehavior.Strict);
        var query = new GetDailyBalanceQuery("m1", new DateOnly(2026, 2, 10));
        var expected = new DailyBalanceReadModel(
            MerchantId: query.MerchantId,
            Date: query.Date,
            Currency: "BRL",
            TotalCredits: 10m,
            TotalDebits: 2m,
            NetBalance: 8m,
            AsOf: DateTimeOffset.Parse("2026-02-10T10:00:00Z"),
            UpdatedAt: DateTimeOffset.Parse("2026-02-10T10:05:00Z"));

        service
            .Setup(x => x.GetDailyAsync(query.MerchantId, query.Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = new GetDailyBalanceHandler(service.Object);

        var result = await sut.Handle(query, CancellationToken.None);

        result.Should().Be(expected);
        service.VerifyAll();
    }
}
