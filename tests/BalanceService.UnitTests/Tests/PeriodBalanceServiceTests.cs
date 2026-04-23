using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Balances.Queries;
using BalanceService.Application.Balances.Queries.Models;
using BalanceService.Application.Balances.Services;
using FluentAssertions;
using Moq;

namespace BalanceService.UnitTests.Tests;

public sealed class PeriodBalanceServiceTests
{
    [Fact]
    public async Task Should_return_zeros_and_default_currency_when_no_items()
    {
        var readRepo = new Mock<IDailyBalanceReadRepository>(MockBehavior.Strict);
        var clock = new Mock<IClock>(MockBehavior.Strict);

        var now = DateTimeOffset.Parse("2026-02-16T03:00:00Z");
        clock.SetupGet(x => x.UtcNow).Returns(now);

        var query = new GetPeriodBalanceQuery("m1", new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 12));
        readRepo.Setup(x => x.ListByPeriodAsync(query.MerchantId, query.From, query.To, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DailyBalanceReadModel>());

        var sut = new PeriodBalanceService(readRepo.Object, clock.Object);

        var result = await sut.GetPeriodAsync(query.MerchantId, query.From, query.To, CancellationToken.None);

        result.MerchantId.Should().Be("m1");
        result.TotalCredits.Should().Be(0m);
        result.TotalDebits.Should().Be(0m);
        result.NetBalance.Should().Be(0m);
        result.Currency.Should().Be("BRL");
        result.Items.Should().BeEmpty();
        result.CalculatedAt.Should().Be(now);

        readRepo.VerifyAll();
        clock.VerifyAll();
    }

    [Fact]
    public async Task Should_sum_items_and_use_first_currency()
    {
        var readRepo = new Mock<IDailyBalanceReadRepository>(MockBehavior.Strict);
        var clock = new Mock<IClock>(MockBehavior.Strict);

        var now = DateTimeOffset.Parse("2026-02-16T03:00:00Z");
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

        result.TotalCredits.Should().Be(10m);
        result.TotalDebits.Should().Be(3m);
        result.NetBalance.Should().Be(7m);
        result.Currency.Should().Be("BRL");
        result.Items.Should().HaveCount(2);
        result.CalculatedAt.Should().Be(now);
    }
}
