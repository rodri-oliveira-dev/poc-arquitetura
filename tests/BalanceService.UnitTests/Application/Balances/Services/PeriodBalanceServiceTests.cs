using System.Globalization;

using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Balances.Queries;
using BalanceService.Application.Balances.Queries.Models;
using BalanceService.Application.Balances.Services;
using Moq;

namespace BalanceService.UnitTests.Application.Balances.Services;

public sealed class PeriodBalanceServiceTests
{
    [Fact]
    public async Task Should_return_zeros_and_default_currency_when_no_items()
    {
        var readRepo = new Mock<IDailyBalanceReadRepository>(MockBehavior.Strict);
        var clock = new Mock<IClock>(MockBehavior.Strict);

        var now = DateTimeOffset.Parse("2026-02-16T03:00:00Z", CultureInfo.InvariantCulture);
        clock.SetupGet(x => x.UtcNow).Returns(now);

        var query = new GetPeriodBalanceQuery("m1", new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 12));
        readRepo.Setup(x => x.ListByPeriodAsync(query.MerchantId, query.From, query.To, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DailyBalanceReadModel>());

        var sut = new PeriodBalanceService(readRepo.Object, clock.Object);

        var result = await sut.GetPeriodAsync(query.MerchantId, query.From, query.To, CancellationToken.None);
        Assert.Equal("m1", result.MerchantId);
        Assert.Equal(0m, result.TotalCredits);
        Assert.Equal(0m, result.TotalDebits);
        Assert.Equal(0m, result.NetBalance);
        Assert.Equal("BRL", result.Currency);
        Assert.Empty(result.Items);
        Assert.Equal(now, result.CalculatedAt);
        readRepo.VerifyAll();
        clock.VerifyAll();
    }

    [Fact]
    public async Task Should_sum_items_and_use_first_currency()
    {
        var readRepo = new Mock<IDailyBalanceReadRepository>(MockBehavior.Strict);
        var clock = new Mock<IClock>(MockBehavior.Strict);

        var now = DateTimeOffset.Parse("2026-02-16T03:00:00Z", CultureInfo.InvariantCulture);
        clock.SetupGet(x => x.UtcNow).Returns(now);

        var query = new GetPeriodBalanceQuery("m1", new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 12));

        var items = new[]
        {
            new DailyBalanceReadModel("m1", new DateOnly(2026,2,10), "BRL", 10m, 0m, 10m, DateTimeOffset.MinValue, now),
            new DailyBalanceReadModel("m1", new DateOnly(2026,2,11), "BRL", 0m, 3m, -3m, DateTimeOffset.MinValue, now),
        };

        readRepo.Setup(x => x.ListByPeriodAsync(query.MerchantId, query.From, query.To, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var sut = new PeriodBalanceService(readRepo.Object, clock.Object);

        var result = await sut.GetPeriodAsync(query.MerchantId, query.From, query.To, CancellationToken.None);
        Assert.Equal(10m, result.TotalCredits);
        Assert.Equal(3m, result.TotalDebits);
        Assert.Equal(7m, result.NetBalance);
        Assert.Equal("BRL", result.Currency);
        Assert.Equal(items, result.Items);
        Assert.Equal(now, result.CalculatedAt);
    }

    [Fact]
    public async Task Should_use_first_item_currency_when_items_exist()
    {
        var readRepo = new Mock<IDailyBalanceReadRepository>(MockBehavior.Strict);
        var clock = new Mock<IClock>(MockBehavior.Strict);

        var now = DateTimeOffset.Parse("2026-02-16T03:00:00Z", CultureInfo.InvariantCulture);
        clock.SetupGet(x => x.UtcNow).Returns(now);

        var query = new GetPeriodBalanceQuery("m1", new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 12));
        var items = new[]
        {
            new DailyBalanceReadModel("m1", new DateOnly(2026,2,10), "USD", 10m, 0m, 10m, DateTimeOffset.MinValue, now),
            new DailyBalanceReadModel("m1", new DateOnly(2026,2,11), "BRL", 0m, 3m, -3m, DateTimeOffset.MinValue, now),
        };

        readRepo.Setup(x => x.ListByPeriodAsync(query.MerchantId, query.From, query.To, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var sut = new PeriodBalanceService(readRepo.Object, clock.Object);

        var result = await sut.GetPeriodAsync(query.MerchantId, query.From, query.To, CancellationToken.None);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(items, result.Items);
    }
}
