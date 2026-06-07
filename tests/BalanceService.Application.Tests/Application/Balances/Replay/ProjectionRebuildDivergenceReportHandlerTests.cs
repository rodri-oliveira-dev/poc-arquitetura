using System.Globalization;

using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Balances.Queries.Models;
using BalanceService.Application.Balances.Replay;
using BalanceService.Application.Contracts.Events;
using BalanceService.Domain.Balances;

using Microsoft.Extensions.Logging;

using Moq;

namespace BalanceService.Application.Tests.Application.Balances.Replay;

public sealed class ProjectionRebuildDivergenceReportHandlerTests
{
    private readonly InMemoryProcessedEventRepository _processedEvents = new();
    private readonly InMemoryDailyBalanceReadRepository _dailyBalances = new();
    private readonly Mock<IClock> _clock = new(MockBehavior.Strict);
    private readonly Mock<ILogger<ProjectionRebuildDivergenceReportHandler>> _logger = new();

    public ProjectionRebuildDivergenceReportHandlerTests()
    {
        _clock.SetupGet(x => x.UtcNow).Returns(Instant("2026-06-07T12:00:00Z"));
    }

    [Fact]
    public async Task Should_report_projection_without_divergence()
    {
        _dailyBalances.Seed("merchant-001", "2026-06-06", "BRL", 10m, 3m);
        var source = Source(
            Candidate("outbox-1", "lan_00000001", "merchant-001", "CREDIT", "10.00"),
            Candidate("outbox-2", "lan_00000002", "merchant-001", "DEBIT", "-3.00"));
        var sut = CreateSut(source);

        var result = await sut.Handle(CreateCommand(), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.False(result.Mutated);
        Assert.False(result.HasDivergences);
        Assert.Equal(7m, item.CurrentBalance);
        Assert.Equal(7m, item.RebuiltBalance);
        Assert.Equal(0m, item.Difference);
        Assert.Equal(2, item.EventsAnalyzed);
        Assert.Equal(0, item.InvalidEvents);
        Assert.Equal(0, item.DuplicateEventsIgnored);
    }

    [Fact]
    public async Task Should_report_projection_with_divergence()
    {
        _dailyBalances.Seed("merchant-001", "2026-06-06", "BRL", 999m, 0m);
        var source = Source(Candidate("outbox-1", "lan_00000001", "merchant-001", "CREDIT", "10.00"));
        var sut = CreateSut(source);

        var result = await sut.Handle(CreateCommand(), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.True(result.HasDivergences);
        Assert.Equal(999m, item.CurrentBalance);
        Assert.Equal(10m, item.RebuiltBalance);
        Assert.Equal(-989m, item.Difference);
    }

    [Fact]
    public async Task Should_include_invalid_event_in_report()
    {
        var source = Source(InvalidCandidate("outbox-invalid", "merchant-001"));
        var sut = CreateSut(source);

        var result = await sut.Handle(CreateCommand(), CancellationToken.None);

        var evt = Assert.Single(result.Events);
        Assert.Equal(ProjectionRebuildEventItemStatus.RejectedInvalidContract, evt.Status);
        Assert.Equal(1, result.TotalInvalid);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Should_ignore_duplicate_when_rebuilding_expected_balance()
    {
        _dailyBalances.Seed("merchant-001", "2026-06-06", "BRL", 10m, 0m);
        var source = Source(
            Candidate("outbox-1", "lan_00000001", "merchant-001", "CREDIT", "10.00"),
            Candidate("outbox-duplicate", "lan_00000001", "merchant-001", "CREDIT", "10.00"));
        var sut = CreateSut(source);

        var result = await sut.Handle(CreateCommand(), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.False(result.HasDivergences);
        Assert.Equal(1, result.TotalDuplicates);
        Assert.Equal(10m, item.RebuiltBalance);
        Assert.Equal(1, item.DuplicateEventsIgnored);
    }

    [Fact]
    public async Task Should_not_change_current_projection_during_dry_run_report()
    {
        _dailyBalances.Seed("merchant-001", "2026-06-06", "BRL", 999m, 0m);
        var before = _dailyBalances.Snapshot();
        var source = Source(Candidate("outbox-1", "lan_00000001", "merchant-001", "CREDIT", "10.00"));
        var sut = CreateSut(source);

        var result = await sut.Handle(CreateCommand(), CancellationToken.None);

        Assert.False(result.Mutated);
        Assert.Equal(before, _dailyBalances.Snapshot());
    }

    private ProjectionRebuildDivergenceReportHandler CreateSut(IFilteredEventReplaySource source)
    {
        var validator = new JsonSchemaEventContractValidator(new EmbeddedEventContractSchemaCatalog());
        var evaluator = new EventReplayMessageEvaluator(validator, _processedEvents);

        return new ProjectionRebuildDivergenceReportHandler(
            source,
            evaluator,
            _dailyBalances,
            _clock.Object,
            _logger.Object);
    }

    private static ProjectionRebuildDivergenceReportCommand CreateCommand()
        => new(
            new PartialProjectionRebuildFilter(
                "merchant-001",
                Instant("2026-06-06T00:00:00Z"),
                Instant("2026-06-07T00:00:00Z")),
            "projection divergence report test");

    private static FakeReplaySource Source(params EventReplaySourceCandidate[] candidates)
        => new(candidates);

    private static EventReplaySourceCandidate Candidate(
        string sourceId,
        string eventId,
        string merchantId,
        string type,
        string amount)
    {
        var instant = Instant("2026-06-06T12:00:00Z");
        return new EventReplaySourceCandidate(
            sourceId,
            Payload(eventId, merchantId, type, amount, instant),
            "LedgerEntryCreated",
            "v2",
            "Outbox",
            instant,
            merchantId,
            null,
            "Processed",
            new Dictionary<string, string>
            {
                ["source"] = "ledger.outbox_messages",
                ["event_type"] = "LedgerEntryCreated.v2"
            });
    }

    private static EventReplaySourceCandidate InvalidCandidate(string sourceId, string merchantId)
    {
        var instant = Instant("2026-06-06T12:00:00Z");
        return new EventReplaySourceCandidate(
            sourceId,
            $$"""
              {
                "id": "lan_invalid",
                "amount": "10.00",
                "currency": "BRL",
                "createdAt": "{{instant:O}}",
                "merchantId": "{{merchantId}}",
                "occurredAt": "{{instant:O}}",
                "correlationId": "2cbdd495-586f-4565-a807-c5dc6710d237"
              }
              """,
            "LedgerEntryCreated",
            "v2",
            "Outbox",
            instant,
            merchantId,
            null,
            "Processed",
            new Dictionary<string, string>
            {
                ["source"] = "ledger.outbox_messages",
                ["event_type"] = "LedgerEntryCreated.v2"
            });
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

    private sealed class FakeReplaySource : IFilteredEventReplaySource
    {
        private readonly IReadOnlyList<EventReplaySourceCandidate> _candidates;

        public FakeReplaySource(params EventReplaySourceCandidate[] candidates)
        {
            _candidates = candidates;
        }

        public Task<IReadOnlyList<EventReplaySourceCandidate>> FindAsync(
            FilteredEventReplayFilter filter,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_candidates);
    }

    private sealed class InMemoryDailyBalanceReadRepository : IDailyBalanceReadRepository
    {
        private readonly List<DailyBalanceReadModel> _items = [];

        public Task<DailyBalanceReadModel?> GetDailyAsync(
            string merchantId,
            DateOnly date,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.MerchantId == merchantId && x.Date == date));

        public Task<IReadOnlyList<DailyBalanceReadModel>> ListByPeriodAsync(
            string merchantId,
            DateOnly from,
            DateOnly to,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DailyBalanceReadModel> result = _items
                .Where(x => x.MerchantId == merchantId && x.Date >= from && x.Date <= to)
                .ToList();
            return Task.FromResult(result);
        }

        public void Seed(string merchantId, string date, string currency, decimal totalCredits, decimal totalDebits)
        {
            var parsedDate = DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            _items.Add(new DailyBalanceReadModel(
                merchantId,
                parsedDate,
                currency,
                totalCredits,
                totalDebits,
                totalCredits - totalDebits,
                Instant($"{date}T12:00:00Z"),
                Instant($"{date}T12:00:00Z")));
        }

        public DailyBalanceReadModel[] Snapshot()
            => _items.ToArray();
    }

    private sealed class InMemoryProcessedEventRepository : IProcessedEventRepository
    {
        public Task<bool> ExistsAsync(string eventId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> TryInsertAsync(ProcessedEvent processedEvent, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<int> DeleteByEventIdsAsync(
            IReadOnlyCollection<string> eventIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
