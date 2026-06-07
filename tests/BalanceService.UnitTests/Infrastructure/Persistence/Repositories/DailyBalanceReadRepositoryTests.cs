using System.Globalization;

using BalanceService.Domain.Balances;
using BalanceService.Infrastructure.Persistence;
using BalanceService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BalanceService.UnitTests.Infrastructure.Persistence.Repositories;

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
        Assert.Null(res);
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
            Currency: "BRL",
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
        Assert.NotNull(res);
        Assert.Equal("m1", res!.MerchantId);
        Assert.Equal(new DateOnly(2026, 2, 10), res.Date);
        Assert.Equal("BRL", res.Currency);
        Assert.Equal(10m, res.TotalCredits);
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
        Assert.Equal(2, res.Count);
        Assert.Equal(new DateOnly(2026, 2, 10), res[0].Date);
        Assert.Equal(new DateOnly(2026, 2, 11), res[1].Date);
    }
}
