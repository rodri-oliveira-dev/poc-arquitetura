using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Balances.Queries;
using BalanceService.Application.Balances.Queries.Models;
using BalanceService.Application.Balances.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BalanceService.UnitTests.Tests;

public sealed class DailyBalanceServiceTests
{
    [Fact]
    public async Task GetDailyAsync_should_return_repository_value_when_found()
    {
        var repo = new Mock<IDailyBalanceReadRepository>(MockBehavior.Strict);
        var clock = new Mock<IClock>(MockBehavior.Strict);

        var query = new GetDailyBalanceQuery("m1", new DateOnly(2026, 2, 10));

        var found = new DailyBalanceReadModel(
            MerchantId: "m1",
            Date: query.Date,
            Currency: "BRL",
            TotalCredits: 10m,
            TotalDebits: 2m,
            NetBalance: 8m,
            AsOf: DateTimeOffset.Parse("2026-02-10T10:00:00Z"),
            UpdatedAt: DateTimeOffset.Parse("2026-02-10T10:05:00Z"));

        repo.Setup(x => x.GetDailyAsync(query.MerchantId, query.Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(found);

        var sut = new DailyBalanceService(repo.Object, clock.Object, NullLogger<DailyBalanceService>.Instance);

        var result = await sut.GetDailyAsync(query.MerchantId, query.Date, CancellationToken.None);

        result.Should().Be(found);
        clock.VerifyNoOtherCalls();
        repo.VerifyAll();
    }

    [Fact]
    public async Task GetDailyAsync_should_return_zeros_when_not_found_and_use_clock_utcnow()
    {
        var repo = new Mock<IDailyBalanceReadRepository>(MockBehavior.Strict);
        var clock = new Mock<IClock>(MockBehavior.Strict);

        var query = new GetDailyBalanceQuery("m1", new DateOnly(2026, 2, 10));

        repo.Setup(x => x.GetDailyAsync(query.MerchantId, query.Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyBalanceReadModel?)null);

        var now = DateTimeOffset.Parse("2026-02-10T12:00:00Z");
        clock.SetupGet(x => x.UtcNow).Returns(now);

        var sut = new DailyBalanceService(repo.Object, clock.Object, NullLogger<DailyBalanceService>.Instance);

        var result = await sut.GetDailyAsync(query.MerchantId, query.Date, CancellationToken.None);

        result.MerchantId.Should().Be("m1");
        result.Date.Should().Be(query.Date);
        result.Currency.Should().Be("BRL");
        result.TotalCredits.Should().Be(0m);
        result.TotalDebits.Should().Be(0m);
        result.NetBalance.Should().Be(0m);
        result.AsOf.Should().Be(DateTimeOffset.MinValue);
        result.UpdatedAt.Should().Be(now);

        repo.VerifyAll();
        clock.VerifyAll();
    }
}
