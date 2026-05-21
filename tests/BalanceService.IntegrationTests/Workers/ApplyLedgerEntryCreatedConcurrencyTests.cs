using System.Globalization;

using BalanceService.Application.Balances.Commands;
using BalanceService.Domain.Balances;
using BalanceService.IntegrationTests.Infrastructure;
using FluentAssertions;
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

        balances.Should().ContainSingle();
        processedEvents.Should().HaveCount(2);
        processedEvents.Select(x => x.EventId).Should().BeEquivalentTo(credit.Id, debit.Id);

        var balance = balances.Single();
        balance.Date.Should().Be(new DateOnly(2026, 2, 16));
        balance.Currency.Should().Be("BRL");
        balance.TotalCredits.Should().Be(100m);
        balance.TotalDebits.Should().Be(35m);
        balance.NetBalance.Should().Be(65m);
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
