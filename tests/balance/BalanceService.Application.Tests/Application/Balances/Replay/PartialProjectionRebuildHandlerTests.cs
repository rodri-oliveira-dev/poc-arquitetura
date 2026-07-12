using System.Globalization;

using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Balances.Replay;
using BalanceService.Application.Contracts.Events;
using BalanceService.Application.Idempotency;
using BalanceService.Domain.Balances;

using Microsoft.Extensions.Logging;

using Moq;

namespace BalanceService.Application.Tests.Application.Balances.Replay;

public sealed class PartialProjectionRebuildHandlerTests
{
    private readonly InMemoryDailyBalanceRepository _dailyBalances = new();
    private readonly InMemoryProcessedEventRepository _processedEvents = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Mock<IClock> _clock = new(MockBehavior.Strict);
    private readonly Mock<ILogger<PartialProjectionRebuildHandler>> _logger = new();

    public PartialProjectionRebuildHandlerTests()
    {
        _clock.SetupGet(x => x.UtcNow).Returns(Instant("2026-06-07T12:00:00Z"));
    }

    [Fact]
    public async Task Should_rebuild_single_merchant_account_projection()
    {
        SeedDailyBalance("merchant-001", "lan_bad001", "CREDIT", "999.00");
        var source = Source(
            Candidate("outbox-2", "lan_00000002", "merchant-001", "DEBIT", "-3.00"),
            Candidate("outbox-1", "lan_00000001", "merchant-001", "CREDIT", "10.00"));
        var sut = CreateSut(source);

        var result = await sut.Handle(CreateCommand(execute: true), CancellationToken.None);

        var balance = Assert.Single(_dailyBalances.Items, x => x.MerchantId == "merchant-001");
        Assert.False(result.DryRun);
        Assert.True(result.Mutated);
        Assert.Equal(2, result.TotalRebuilt);
        Assert.Equal(1, result.TotalDailyBalancesDeleted);
        Assert.Equal(10m, balance.TotalCredits);
        Assert.Equal(3m, balance.TotalDebits);
        Assert.Equal(7m, balance.NetBalance);
        Assert.True(_processedEvents.Contains("lan_00000001"));
        Assert.True(_processedEvents.Contains("lan_00000002"));
    }

    [Fact]
    public async Task Should_not_affect_other_merchants()
    {
        SeedDailyBalance("merchant-001", "lan_bad001", "CREDIT", "999.00");
        SeedDailyBalance("merchant-002", "lan_other1", "CREDIT", "25.00");
        var source = Source(Candidate("outbox-1", "lan_00000001", "merchant-001", "CREDIT", "10.00"));
        var sut = CreateSut(source);

        await sut.Handle(CreateCommand(execute: true), CancellationToken.None);

        var other = Assert.Single(_dailyBalances.Items, x => x.MerchantId == "merchant-002");
        Assert.Equal(25m, other.TotalCredits);
        Assert.Equal(25m, other.NetBalance);
    }

    [Fact]
    public async Task Should_not_apply_duplicate_events_twice()
    {
        var source = Source(
            Candidate("outbox-1", "lan_00000001", "merchant-001", "CREDIT", "10.00"),
            Candidate("outbox-duplicate", "lan_00000001", "merchant-001", "CREDIT", "10.00"));
        var sut = CreateSut(source);

        var result = await sut.Handle(CreateCommand(execute: true), CancellationToken.None);

        var balance = Assert.Single(_dailyBalances.Items);
        Assert.Equal(1, result.TotalDuplicates);
        Assert.Equal(1, result.TotalRebuilt);
        Assert.Equal(10m, balance.TotalCredits);
        Assert.Equal(10m, balance.NetBalance);
    }

    [Fact]
    public async Task Should_reject_invalid_event_without_mutating_projection()
    {
        SeedDailyBalance("merchant-001", "lan_bad001", "CREDIT", "999.00");
        var source = Source(Candidate(
            "outbox-invalid",
            "lan_00000001",
            "merchant-001",
            "CREDIT",
            "-10.00"));
        var sut = CreateSut(source);

        var result = await sut.Handle(CreateCommand(execute: true), CancellationToken.None);

        var balance = Assert.Single(_dailyBalances.Items);
        Assert.False(result.Mutated);
        Assert.Equal(1, result.TotalInvalid);
        Assert.Equal(1, result.TotalRejected);
        Assert.Equal(999m, balance.TotalCredits);
        Assert.False(_processedEvents.Contains("lan_00000001"));
    }

    [Fact]
    public async Task Should_rebuild_deterministically_when_reexecuted()
    {
        var source = Source(
            Candidate("outbox-3", "lan_00000003", "merchant-001", "CREDIT", "2.00", "2026-06-06T12:00:02Z"),
            Candidate("outbox-1", "lan_00000001", "merchant-001", "CREDIT", "10.00", "2026-06-06T12:00:00Z"),
            Candidate("outbox-2", "lan_00000002", "merchant-001", "DEBIT", "-3.00", "2026-06-06T12:00:01Z"));
        var sut = CreateSut(source);

        await sut.Handle(CreateCommand(execute: true), CancellationToken.None);
        var first = Snapshot("merchant-001");

        await sut.Handle(CreateCommand(execute: true), CancellationToken.None);
        var second = Snapshot("merchant-001");

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Should_dry_run_without_mutating()
    {
        SeedDailyBalance("merchant-001", "lan_bad001", "CREDIT", "999.00");
        var source = Source(Candidate("outbox-1", "lan_00000001", "merchant-001", "CREDIT", "10.00"));
        var sut = CreateSut(source);

        var result = await sut.Handle(CreateCommand(execute: false), CancellationToken.None);

        var balance = Assert.Single(_dailyBalances.Items);
        Assert.True(result.DryRun);
        Assert.False(result.Mutated);
        Assert.Equal(1, result.TotalEligible);
        Assert.Equal(999m, balance.TotalCredits);
    }

    private PartialProjectionRebuildHandler CreateSut(IFilteredEventReplaySource source)
    {
        var validator = new JsonSchemaEventContractValidator(new EmbeddedEventContractSchemaCatalog());
        var evaluator = new EventReplayMessageEvaluator(validator, _processedEvents);

        return new PartialProjectionRebuildHandler(
            source,
            evaluator,
            _dailyBalances,
            _processedEvents,
            _unitOfWork,
            _clock.Object,
            _logger.Object);
    }

    private static PartialProjectionRebuildCommand CreateCommand(bool execute)
        => new(
            new PartialProjectionRebuildFilter(
                "merchant-001",
                Instant("2026-06-06T00:00:00Z"),
                Instant("2026-06-07T00:00:00Z")),
            "partial rebuild test",
            execute);

    private (decimal Credits, decimal Debits, decimal Net) Snapshot(string merchantId)
    {
        var balance = Assert.Single(_dailyBalances.Items, x => x.MerchantId == merchantId);
        return (balance.TotalCredits, balance.TotalDebits, balance.NetBalance);
    }

    private void SeedDailyBalance(
        string merchantId,
        string eventId,
        string type,
        string amount)
    {
        var now = Instant("2026-06-07T12:00:00Z");
        var balance = new DailyBalance(merchantId, new DateOnly(2026, 6, 6), "BRL", now);
        balance.Apply(ToMovement(merchantId, type, amount), now);

        _dailyBalances.Items.Add(balance);
        _processedEvents.Seed(eventId);
    }

    private static FakeReplaySource Source(params EventReplaySourceCandidate[] candidates)
        => new(candidates);

    private static EventReplaySourceCandidate Candidate(
        string sourceId,
        string eventId,
        string merchantId,
        string type,
        string amount,
        string occurredAt = "2026-06-06T12:00:00Z")
    {
        var instant = Instant(occurredAt);
        return new EventReplaySourceCandidate(
            new EventReplaySourcePosition(sourceId, instant, "Processed"),
            new EventReplayPayload(
                Payload(eventId, merchantId, type, amount, instant),
                new Dictionary<string, string>
                {
                    ["source"] = "ledger.outbox_messages",
                    ["event_type"] = "LedgerEntryCreated.v2"
                }),
            new EventReplayContract("LedgerEntryCreated", "v2", "Outbox"),
            new EventReplaySubject(merchantId, null));
    }

    private static string Payload(
        string eventId,
        string merchantId,
        string type,
        string amount,
        DateTimeOffset occurredAt)
        => $$"""
            {
              "id": "{{eventId}}",
              "type": "{{type}}",
              "amount": "{{amount}}",
              "currency": "BRL",
              "createdAt": "{{occurredAt:O}}",
              "merchantId": "{{merchantId}}",
              "occurredAt": "{{occurredAt:O}}",
              "description": "Venda aprovada",
              "correlationId": "2cbdd495-586f-4565-a807-c5dc6710d237",
              "externalReference": "order-123"
            }
            """;

    private static DateTimeOffset Instant(string value)
        => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    private static BalanceMovement ToMovement(string merchantId, string type, string amount)
        => new(
            merchantId,
            new DateOnly(2026, 6, 6),
            new Currency("BRL"),
            type == "CREDIT" ? BalanceMovementType.Credit : BalanceMovementType.Debit,
            BalanceAmount.ParseInvariant(amount),
            Instant("2026-06-06T12:00:00Z"));

    private sealed class FakeReplaySource(params EventReplaySourceCandidate[] candidates) : IFilteredEventReplaySource
    {
        private readonly IReadOnlyList<EventReplaySourceCandidate> _candidates = candidates;

        public Task<IReadOnlyList<EventReplaySourceCandidate>> FindAsync(
            FilteredEventReplayFilter filter,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_candidates);
    }

    private sealed class InMemoryDailyBalanceRepository : IDailyBalanceRepository
    {
        public List<DailyBalance> Items { get; } = [];

        public Task LockByMerchantDateAndCurrencyAsync(
            string merchantId,
            DateOnly date,
            string currency,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<DailyBalance?> GetByMerchantDateAndCurrencyAsync(
            string merchantId,
            DateOnly date,
            string currency,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.MerchantId == merchantId &&
                x.Date == date &&
                x.Currency == currency));

        public Task AddAsync(DailyBalance dailyBalance, CancellationToken cancellationToken = default)
        {
            Items.Add(dailyBalance);
            return Task.CompletedTask;
        }

        public Task<int> DeleteByMerchantAndDateRangeAsync(
            string merchantId,
            DateOnly from,
            DateOnly until,
            CancellationToken cancellationToken = default)
        {
            var removed = Items.RemoveAll(x => x.MerchantId == merchantId && x.Date >= from && x.Date <= until);
            return Task.FromResult(removed);
        }
    }

    private sealed class InMemoryProcessedEventRepository : IProcessedEventRepository
    {
        private readonly HashSet<string> _eventIds = new(StringComparer.Ordinal);

        public bool Contains(string eventId) => _eventIds.Contains(eventId);

        public void Seed(string eventId) => _eventIds.Add(eventId);

        public Task<bool> ExistsAsync(string eventId, CancellationToken cancellationToken = default)
            => Task.FromResult(_eventIds.Contains(eventId));

        public Task<bool> TryInsertAsync(ProcessedEvent processedEvent, CancellationToken cancellationToken = default)
            => Task.FromResult(_eventIds.Add(processedEvent.EventId));

        public Task<int> DeleteByEventIdsAsync(
            IReadOnlyCollection<string> eventIds,
            CancellationToken cancellationToken = default)
        {
            var deleted = eventIds.Count(_eventIds.Remove);
            return Task.FromResult(deleted);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IAppTransaction>(new FakeTransaction());

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1);
    }

    private sealed class FakeTransaction : IAppTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
