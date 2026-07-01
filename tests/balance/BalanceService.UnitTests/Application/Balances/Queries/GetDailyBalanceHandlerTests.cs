using System.Globalization;

using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Balances.Queries;
using BalanceService.Application.Balances.Queries.Models;

using Moq;

namespace BalanceService.UnitTests.Application.Balances.Queries;

public sealed class GetDailyBalanceHandlerTests
{
    [Fact]
    public async Task Handle_should_return_repository_value_when_found()
    {
        var repo = new Mock<IDailyBalanceReadRepository>(MockBehavior.Strict);
        var clock = new Mock<IClock>(MockBehavior.Strict);
        var query = new GetDailyBalanceQuery("m1", new DateOnly(2026, 2, 10));
        var found = new DailyBalanceReadModel(
            MerchantId: query.MerchantId,
            Date: query.Date,
            Currency: "BRL",
            TotalCredits: 10m,
            TotalDebits: 2m,
            NetBalance: 8m,
            AsOf: DateTimeOffset.Parse("2026-02-10T10:00:00Z", CultureInfo.InvariantCulture),
            UpdatedAt: DateTimeOffset.Parse("2026-02-10T10:05:00Z", CultureInfo.InvariantCulture));

        repo.Setup(x => x.GetDailyAsync(query.MerchantId, query.Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(found);

        var sut = new GetDailyBalanceHandler(repo.Object, clock.Object);

        var result = await sut.Handle(query, CancellationToken.None);
        Assert.Equal(found, result);
        clock.VerifyNoOtherCalls();
        repo.VerifyAll();
    }

    [Fact]
    public async Task Handle_should_return_zeros_when_not_found_and_use_clock_utcnow()
    {
        var repo = new Mock<IDailyBalanceReadRepository>(MockBehavior.Strict);
        var clock = new Mock<IClock>(MockBehavior.Strict);
        var query = new GetDailyBalanceQuery("m1", new DateOnly(2026, 2, 10));

        repo.Setup(x => x.GetDailyAsync(query.MerchantId, query.Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyBalanceReadModel?)null);

        var now = DateTimeOffset.Parse("2026-02-10T12:00:00Z", CultureInfo.InvariantCulture);
        clock.SetupGet(x => x.UtcNow).Returns(now);

        var sut = new GetDailyBalanceHandler(repo.Object, clock.Object);

        var result = await sut.Handle(query, CancellationToken.None);
        Assert.Equal("m1", result.MerchantId);
        Assert.Equal(query.Date, result.Date);
        Assert.Equal("BRL", result.Currency);
        Assert.Equal(0m, result.TotalCredits);
        Assert.Equal(0m, result.TotalDebits);
        Assert.Equal(0m, result.NetBalance);
        Assert.Equal(DateTimeOffset.MinValue, result.AsOf);
        Assert.Equal(now, result.UpdatedAt);
        repo.VerifyAll();
        clock.VerifyAll();
    }
}
