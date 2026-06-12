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
            .ToListAsync(TestContext.Current.CancellationToken);

        var processedEvents = await db.ProcessedEvents
            .Where(x => x.MerchantId == merchantId)
            .ToListAsync(TestContext.Current.CancellationToken);
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

    [Fact]
    public async Task Should_apply_same_event_once_under_concurrency()
    {
        await _fixture.CleanAsync();

        var now = DateTimeOffset.Parse("2026-02-16T15:00:00Z", CultureInfo.InvariantCulture);
        using var serviceProvider = _fixture.CreateServiceProvider(now);
        var merchantId = $"merchant-{Guid.NewGuid():N}";
        var evt = CreateEvent(
            id: $"evt-{Guid.NewGuid():N}",
            merchantId: merchantId,
            type: "CREDIT",
            amount: "100.00",
            occurredAt: DateTimeOffset.Parse("2026-02-16T10:00:00-03:00", CultureInfo.InvariantCulture));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = ApplyAsync(serviceProvider, evt, start.Task);
        var second = ApplyAsync(serviceProvider, evt, start.Task);

        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, x => x == ApplyLedgerEntryCreatedResult.Processed);
        Assert.Single(results, x => x == ApplyLedgerEntryCreatedResult.IgnoredDuplicate);

        await using var db = _fixture.CreateDbContext();
        var balance = await db.DailyBalances.SingleAsync(x => x.MerchantId == merchantId, TestContext.Current.CancellationToken);
        Assert.Equal(100m, balance.TotalCredits);
        Assert.Equal(0m, balance.TotalDebits);
        Assert.Equal(100m, balance.NetBalance);
        Assert.Equal(1, await db.ProcessedEvents.CountAsync(x => x.EventId == evt.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ignore_sequential_duplicate_without_changing_projection_state()
    {
        await _fixture.CleanAsync();

        var cancellationToken = TestContext.Current.CancellationToken;
        var firstNow = DateTimeOffset.Parse("2026-02-16T15:00:00Z", CultureInfo.InvariantCulture);
        var duplicateNow = DateTimeOffset.Parse("2026-02-16T16:00:00Z", CultureInfo.InvariantCulture);
        var merchantId = $"merchant-{Guid.NewGuid():N}";
        var evt = CreateEvent(
            id: $"evt-{Guid.NewGuid():N}",
            merchantId: merchantId,
            type: "CREDIT",
            amount: "100.00",
            occurredAt: DateTimeOffset.Parse("2026-02-16T10:00:00-03:00", CultureInfo.InvariantCulture));

        using (var serviceProvider = _fixture.CreateServiceProvider(firstNow))
        {
            var firstResult = await ApplyAsync(serviceProvider, evt, Task.CompletedTask);
            Assert.Equal(ApplyLedgerEntryCreatedResult.Processed, firstResult);
        }

        await using var firstDb = _fixture.CreateDbContext();
        var firstBalance = await firstDb.DailyBalances.SingleAsync(x => x.MerchantId == merchantId, cancellationToken);
        var firstUpdatedAt = firstBalance.UpdatedAt;
        var firstAsOf = firstBalance.AsOf;
        var processedAt = await firstDb.ProcessedEvents
            .Where(x => x.EventId == evt.Id)
            .Select(x => x.ProcessedAt)
            .SingleAsync(cancellationToken);

        using (var serviceProvider = _fixture.CreateServiceProvider(duplicateNow))
        {
            var duplicateResult = await ApplyAsync(serviceProvider, evt, Task.CompletedTask);
            Assert.Equal(ApplyLedgerEntryCreatedResult.IgnoredDuplicate, duplicateResult);
        }

        await using var db = _fixture.CreateDbContext();
        var balance = await db.DailyBalances.SingleAsync(x => x.MerchantId == merchantId, cancellationToken);

        Assert.Equal(100m, balance.TotalCredits);
        Assert.Equal(0m, balance.TotalDebits);
        Assert.Equal(100m, balance.NetBalance);
        Assert.Equal(firstUpdatedAt, balance.UpdatedAt);
        Assert.Equal(firstAsOf, balance.AsOf);
        Assert.Equal(1, await db.ProcessedEvents.CountAsync(x => x.EventId == evt.Id, cancellationToken));
        Assert.Equal(firstNow, processedAt);
    }

    [Fact]
    public async Task Should_apply_same_content_again_when_event_id_is_different()
    {
        await _fixture.CleanAsync();

        var cancellationToken = TestContext.Current.CancellationToken;
        var firstNow = DateTimeOffset.Parse("2026-02-16T15:00:00Z", CultureInfo.InvariantCulture);
        var secondNow = DateTimeOffset.Parse("2026-02-16T16:00:00Z", CultureInfo.InvariantCulture);
        var merchantId = $"merchant-{Guid.NewGuid():N}";
        var first = CreateEvent(
            id: $"evt-{Guid.NewGuid():N}",
            merchantId: merchantId,
            type: "CREDIT",
            amount: "100.00",
            occurredAt: DateTimeOffset.Parse("2026-02-16T10:00:00-03:00", CultureInfo.InvariantCulture));
        var second = first with { Id = $"evt-{Guid.NewGuid():N}" };

        using (var serviceProvider = _fixture.CreateServiceProvider(firstNow))
        {
            var firstResult = await ApplyAsync(serviceProvider, first, Task.CompletedTask);
            Assert.Equal(ApplyLedgerEntryCreatedResult.Processed, firstResult);
        }

        using (var serviceProvider = _fixture.CreateServiceProvider(secondNow))
        {
            var secondResult = await ApplyAsync(serviceProvider, second, Task.CompletedTask);
            Assert.Equal(ApplyLedgerEntryCreatedResult.Processed, secondResult);
        }

        await using var db = _fixture.CreateDbContext();
        var balance = await db.DailyBalances.SingleAsync(x => x.MerchantId == merchantId, cancellationToken);

        Assert.Equal(200m, balance.TotalCredits);
        Assert.Equal(0m, balance.TotalDebits);
        Assert.Equal(200m, balance.NetBalance);
        Assert.Equal(first.OccurredAt.ToUniversalTime(), balance.AsOf);
        Assert.Equal(secondNow, balance.UpdatedAt);
        Assert.Equal(2, await db.ProcessedEvents.CountAsync(x => x.MerchantId == merchantId, cancellationToken));
        Assert.True(await db.ProcessedEvents.AnyAsync(x => x.EventId == first.Id, cancellationToken));
        Assert.True(await db.ProcessedEvents.AnyAsync(x => x.EventId == second.Id, cancellationToken));
    }

    private static async Task<ApplyLedgerEntryCreatedResult> ApplyAsync(
        IServiceProvider serviceProvider,
        LedgerEntryCreatedEvent evt,
        Task start)
    {
        await start;

        await using var scope = serviceProvider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ApplyLedgerEntryCreatedHandler>();

        return await handler.Handle(new ApplyLedgerEntryCreatedCommand(evt), TestContext.Current.CancellationToken);
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
            "BRL",
            occurredAt,
            merchantId,
            occurredAt,
            Description: null,
            CorrelationId: $"corr-{id}",
            ExternalReference: null);
}
