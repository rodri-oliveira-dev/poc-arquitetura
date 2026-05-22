using System.Globalization;

using BalanceService.Application.Balances.Commands;
using BalanceService.Domain.Balances;
using BalanceService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BalanceService.IntegrationTests.Workers;

[Collection(PostgresBalanceCollection.Name)]
public sealed class ApplyLedgerEntryCreatedConcurrencyTests
{
    private readonly PostgresBalanceFixture _fixture;

    public ApplyLedgerEntryCreatedConcurrencyTests(PostgresBalanceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Should_accumulate_distinct_events_for_same_daily_balance_under_concurrency()
    {
        await _fixture.CleanAsync();

        var now = DateTimeOffset.Parse("2026-02-16T15:00:00Z", CultureInfo.InvariantCulture);
        using var serviceProvider = _fixture.CreateServiceProvider(now);

        var merchantId = $"merchant-{Guid.NewGuid():N}";
        var credit = CreateEvent(
            id: $"evt-{Guid.NewGuid():N}",
            merchantId: merchantId,
            type: "CREDIT",
            amount: "100.00",
            occurredAt: DateTimeOffset.Parse("2026-02-16T10:00:00-03:00", CultureInfo.InvariantCulture));

        var debit = CreateEvent(
            id: $"evt-{Guid.NewGuid():N}",
            merchantId: merchantId,
            type: "DEBIT",
            amount: "-35.00",
            occurredAt: DateTimeOffset.Parse("2026-02-16T10:01:00-03:00", CultureInfo.InvariantCulture));

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = new[]
        {
            ApplyAsync(serviceProvider, credit, start.Task),
            ApplyAsync(serviceProvider, debit, start.Task)
        };

        start.SetResult();
        await Task.WhenAll(tasks);

        await using var db = _fixture.CreateDbContext();
        var balances = await db.DailyBalances
            .Where(x => x.MerchantId == merchantId)
            .ToListAsync();

        var processedEvents = await db.ProcessedEvents
            .Where(x => x.MerchantId == merchantId)
            .ToListAsync();
        Assert.Single(balances);
        Assert.Equal(2, processedEvents.Count);
        Assert.Equivalent(new[] { credit.Id, debit.Id }, processedEvents.Select(x => x.EventId));

        var balance = balances.Single();
        Assert.Equal(new DateOnly(2026, 2, 16), balance.Date);
        Assert.Equal("BRL", balance.Currency);
        Assert.Equal(100m, balance.TotalCredits);
        Assert.Equal(35m, balance.TotalDebits);
        Assert.Equal(65m, balance.NetBalance);
    }

    private static async Task ApplyAsync(
        IServiceProvider serviceProvider,
        LedgerEntryCreatedEvent evt,
        Task start)
    {
        await start;

        await using var scope = serviceProvider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ApplyLedgerEntryCreatedHandler>();

        await handler.Handle(new ApplyLedgerEntryCreatedCommand(evt), CancellationToken.None);
    }

    private static LedgerEntryCreatedEvent CreateEvent(
        string id,
        string merchantId,
        string type,
        string amount,
        DateTimeOffset occurredAt)
        => new(
            id,
            type,
            amount,
            occurredAt,
            merchantId,
            occurredAt,
            Description: null,
            CorrelationId: $"corr-{id}",
            ExternalReference: null);
}
