using System.Globalization;

using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Balances.Queries;
using BalanceService.Application.Balances.Queries.Models;

using Moq;

namespace BalanceService.UnitTests.Application.Balances.Queries;

public sealed class GetPeriodBalanceHandlerTests
{
    [Fact]
    public async Task Handle_should_return_zeros_and_default_currency_when_no_items()
    {
        var readRepo = new Mock<IDailyBalanceReadRepository>(MockBehavior.Strict);
        var timeProvider = new FixedTimeProvider();
        var now = DateTimeOffset.Parse("2026-02-16T03:00:00Z", CultureInfo.InvariantCulture);
        timeProvider.SetUtcNow(now);

        var query = new GetPeriodBalanceQuery("m1", new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 12));
        readRepo.Setup(x => x.ListByPeriodAsync(query.MerchantId, query.From, query.To, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DailyBalanceReadModel>());

        var sut = new GetPeriodBalanceHandler(readRepo.Object, timeProvider);

        var result = await sut.Handle(query, CancellationToken.None);
        Assert.Equal("m1", result.MerchantId);
        Assert.Equal(0m, result.TotalCredits);
        Assert.Equal(0m, result.TotalDebits);
        Assert.Equal(0m, result.NetBalance);
        Assert.Equal("BRL", result.Currency);
        Assert.Empty(result.Items);
        Assert.Equal(now, result.CalculatedAt);
        readRepo.VerifyAll();
    }

    [Fact]
    public async Task Handle_should_sum_items_and_use_first_currency()
    {
        var readRepo = new Mock<IDailyBalanceReadRepository>(MockBehavior.Strict);
        var timeProvider = new FixedTimeProvider();
        var now = DateTimeOffset.Parse("2026-02-16T03:00:00Z", CultureInfo.InvariantCulture);
        timeProvider.SetUtcNow(now);

        var query = new GetPeriodBalanceQuery("m1", new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 12));
        var items = new[]
        {
            new DailyBalanceReadModel("m1", new DateOnly(2026, 2, 10), "USD", 10m, 0m, 10m, DateTimeOffset.MinValue, now),
            new DailyBalanceReadModel("m1", new DateOnly(2026, 2, 11), "BRL", 0m, 3m, -3m, DateTimeOffset.MinValue, now),
        };

        readRepo.Setup(x => x.ListByPeriodAsync(query.MerchantId, query.From, query.To, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var sut = new GetPeriodBalanceHandler(readRepo.Object, timeProvider);

        var result = await sut.Handle(query, CancellationToken.None);
        Assert.Equal(10m, result.TotalCredits);
        Assert.Equal(3m, result.TotalDebits);
        Assert.Equal(7m, result.NetBalance);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(items, result.Items);
        Assert.Equal(now, result.CalculatedAt);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 2, 16, 12, 0, 0, TimeSpan.Zero);

        public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
