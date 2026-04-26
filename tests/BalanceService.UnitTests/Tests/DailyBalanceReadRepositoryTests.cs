using System.Globalization;

using BalanceService.Domain.Balances;
using BalanceService.Infrastructure.Persistence;
using BalanceService.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BalanceService.UnitTests.Tests;

public sealed class DailyBalanceReadRepositoryTests
{
    [Fact]
    public async Task GetDailyAsync_should_return_null_when_not_found()
    {
        var options = new DbContextOptionsBuilder<BalanceDbContext>()
            .UseInMemoryDatabase($"balance-repo-{Guid.NewGuid():N}")
            .Options;

        await using var db = new BalanceDbContext(options);
        var sut = new DailyBalanceReadRepository(db);

        var res = await sut.GetDailyAsync("m1", new DateOnly(2026, 2, 10));
        res.Should().BeNull();
    }

    [Fact]
    public async Task GetDailyAsync_should_project_to_readmodel_when_found()
    {
        var options = new DbContextOptionsBuilder<BalanceDbContext>()
            .UseInMemoryDatabase($"balance-repo-{Guid.NewGuid():N}")
            .Options;

        var now = DateTimeOffset.Parse("2026-02-10T12:00:00Z", CultureInfo.InvariantCulture);
        await using var db = new BalanceDbContext(options);

        var entity = new DailyBalance("m1", new DateOnly(2026, 2, 10), "BRL", now);
        // Força valores não-zero via evento
        entity.Apply(new LedgerEntryCreatedEvent(
            Id: Guid.NewGuid().ToString(),
            Type: "CREDIT",
            Amount: "10.00",
            CreatedAt: DateTimeOffset.Parse("2026-02-10T09:59:00Z", CultureInfo.InvariantCulture),
            MerchantId: "m1",
            OccurredAt: DateTimeOffset.Parse("2026-02-10T10:00:00Z", CultureInfo.InvariantCulture),
            Description: null,
            CorrelationId: Guid.NewGuid().ToString(),
            ExternalReference: null), now);

        db.DailyBalances.Add(entity);
        await db.SaveChangesAsync();

        var sut = new DailyBalanceReadRepository(db);
        var res = await sut.GetDailyAsync("m1", new DateOnly(2026, 2, 10));

        res.Should().NotBeNull();
        res!.MerchantId.Should().Be("m1");
        res.Date.Should().Be(new DateOnly(2026, 2, 10));
        res.Currency.Should().Be("BRL");
        res.TotalCredits.Should().Be(10m);
    }

    [Fact]
    public async Task ListByPeriodAsync_should_return_ordered_items()
    {
        var options = new DbContextOptionsBuilder<BalanceDbContext>()
            .UseInMemoryDatabase($"balance-repo-{Guid.NewGuid():N}")
            .Options;

        var now = DateTimeOffset.Parse("2026-02-10T12:00:00Z", CultureInfo.InvariantCulture);
        await using var db = new BalanceDbContext(options);

        db.DailyBalances.Add(new DailyBalance("m1", new DateOnly(2026, 2, 11), "BRL", now));
        db.DailyBalances.Add(new DailyBalance("m1", new DateOnly(2026, 2, 10), "BRL", now));
        db.DailyBalances.Add(new DailyBalance("m2", new DateOnly(2026, 2, 10), "BRL", now));
        await db.SaveChangesAsync();

        var sut = new DailyBalanceReadRepository(db);
        var res = await sut.ListByPeriodAsync("m1", new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 11));

        res.Should().HaveCount(2);
        res[0].Date.Should().Be(new DateOnly(2026, 2, 10));
        res[1].Date.Should().Be(new DateOnly(2026, 2, 11));
    }
}
