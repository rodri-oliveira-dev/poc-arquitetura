using System.Globalization;

using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Balances.Replay;
using BalanceService.Application.Contracts.Events;

using MediatR;

using Microsoft.Extensions.Logging;

using Moq;

namespace BalanceService.Application.Tests.Application.Balances.Replay;

public sealed class FilteredEventReplayHandlerTests
{
    private readonly Mock<IProcessedEventRepository> _processedEvents = new(MockBehavior.Strict);
    private readonly Mock<ISender> _sender = new(MockBehavior.Strict);
    private readonly Mock<ILogger<FilteredEventReplayHandler>> _logger = new();

    [Fact]
    public async Task Should_dry_run_without_reprocessing()
    {
        var source = new FakeReplaySource(
            Candidate("outbox-1", "lan_00000001", occurredAt: Instant("2026-06-06T10:00:00Z")));
        var sut = CreateSut(source);
        SetupNotProcessed();

        FilteredEventReplayResult result = await sut.Handle(CreateCommand(), CancellationToken.None);

        Assert.True(result.DryRun);
        Assert.Equal(1, result.TotalFound);
        Assert.Equal(1, result.TotalValid);
        Assert.Equal(0, result.TotalInvalid);
        Assert.Equal(0, result.TotalAlreadyProcessed);
        Assert.Equal(1, result.TotalEligible);
        Assert.Equal(0, result.TotalRejected);
        Assert.Equal(0, result.TotalReplayed);
        Assert.Equal(FilteredEventReplayItemStatus.Eligible, Assert.Single(result.Items).Status);
        _sender.Verify(
            x => x.Send(It.IsAny<ManualEventReplayCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Should_filter_by_period()
    {
        var source = new FakeReplaySource(
            Candidate("outbox-before", "lan_00000001", occurredAt: Instant("2026-06-05T23:59:59Z")),
            Candidate("outbox-inside", "lan_00000002", occurredAt: Instant("2026-06-06T12:00:00Z")),
            Candidate("outbox-after", "lan_00000003", occurredAt: Instant("2026-06-07T00:00:01Z")));
        var sut = CreateSut(source);
        SetupNotProcessed();

        var result = await sut.Handle(
            CreateCommand(filter: new FilteredEventReplayFilter(
                EventName: null,
                EventVersion: null,
                OccurredFrom: Instant("2026-06-06T00:00:00Z"),
                OccurredUntil: Instant("2026-06-07T00:00:00Z"),
                MerchantId: null,
                AccountId: null,
                Status: null)),
            CancellationToken.None);

        Assert.Equal(1, result.TotalFound);
        Assert.Equal("outbox-inside", Assert.Single(result.Items).SourceId);
    }

    [Fact]
    public async Task Should_filter_by_event_name()
    {
        var source = new FakeReplaySource(
            Candidate("outbox-ledger", "lan_00000001", eventName: "LedgerEntryCreated"),
            Candidate("outbox-other", "rep_1", eventName: "ReprocessamentoLancamentosSolicitado"));
        var sut = CreateSut(source);
        SetupNotProcessed();

        var result = await sut.Handle(
            CreateCommand(filter: new FilteredEventReplayFilter(
                EventName: "LedgerEntryCreated",
                EventVersion: null,
                OccurredFrom: null,
                OccurredUntil: null,
                MerchantId: null,
                AccountId: null,
                Status: null)),
            CancellationToken.None);

        Assert.Equal(1, result.TotalFound);
        Assert.Equal("outbox-ledger", Assert.Single(result.Items).SourceId);
    }

    [Fact]
    public async Task Should_reject_invalid_messages()
    {
        var source = new FakeReplaySource(
            Candidate("outbox-invalid", "lan_invalid", payload: InvalidPayload("lan_invalid")));
        var sut = CreateSut(source);

        var result = await sut.Handle(CreateCommand(), CancellationToken.None);

        Assert.Equal(1, result.TotalFound);
        Assert.Equal(0, result.TotalValid);
        Assert.Equal(1, result.TotalInvalid);
        Assert.Equal(0, result.TotalEligible);
        Assert.Equal(1, result.TotalRejected);
        Assert.Equal(FilteredEventReplayItemStatus.RejectedInvalidContract, Assert.Single(result.Items).Status);
        _processedEvents.VerifyNoOtherCalls();
        _sender.Verify(
            x => x.Send(It.IsAny<ManualEventReplayCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Should_ignore_duplicate_messages()
    {
        var source = new FakeReplaySource(
            Candidate("outbox-1", "lan_00000001"),
            Candidate("outbox-2", "lan_00000001"));
        var sut = CreateSut(source);
        SetupNotProcessed();

        var result = await sut.Handle(CreateCommand(), CancellationToken.None);

        Assert.Equal(2, result.TotalFound);
        Assert.Equal(2, result.TotalValid);
        Assert.Equal(1, result.TotalAlreadyProcessed);
        Assert.Equal(1, result.TotalEligible);
        Assert.Contains(result.Items, x => x.SourceId == "outbox-1" && x.Status == FilteredEventReplayItemStatus.Eligible);
        Assert.Contains(result.Items, x => x.SourceId == "outbox-2" && x.Status == FilteredEventReplayItemStatus.AlreadyProcessed);
    }

    [Fact]
    public async Task Should_effectively_replay_eligible_messages_when_requested()
    {
        var source = new FakeReplaySource(
            Candidate("outbox-1", "lan_00000001"));
        var sut = CreateSut(source);
        SetupNotProcessed();

        _sender
            .Setup(x => x.Send(
                It.Is<ManualEventReplayCommand>(command =>
                    command.Payload.Contains("lan_00000001", StringComparison.Ordinal) &&
                    command.EventName == "LedgerEntryCreated" &&
                    command.EventVersion == "v2" &&
                    command.Provider == "Outbox"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ManualEventReplayResult.Replayed("manual-replay", "lan_00000001"));

        var result = await sut.Handle(CreateCommand(execute: true), CancellationToken.None);

        Assert.False(result.DryRun);
        Assert.Equal(1, result.TotalFound);
        Assert.Equal(1, result.TotalValid);
        Assert.Equal(1, result.TotalEligible);
        Assert.Equal(1, result.TotalReplayed);
        Assert.Equal(0, result.TotalRejected);
        Assert.Equal(FilteredEventReplayItemStatus.Replayed, Assert.Single(result.Items).Status);
        _sender.VerifyAll();
    }

    private FilteredEventReplayHandler CreateSut(FakeReplaySource source)
    {
        var validator = new JsonSchemaEventContractValidator(new EmbeddedEventContractSchemaCatalog());
        var evaluator = new EventReplayMessageEvaluator(validator, _processedEvents.Object);

        return new FilteredEventReplayHandler(
            source,
            evaluator,
            _sender.Object,
            _logger.Object);
    }

    private void SetupNotProcessed()
    {
        _processedEvents
            .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private static FilteredEventReplayCommand CreateCommand(
        FilteredEventReplayFilter? filter = null,
        bool execute = false)
        => new(
            filter ?? new FilteredEventReplayFilter(
                EventName: null,
                EventVersion: null,
                OccurredFrom: null,
                OccurredUntil: null,
                MerchantId: null,
                AccountId: null,
                Status: null),
            "operational replay after investigation",
            execute);

    private static EventReplaySourceCandidate Candidate(
        string sourceId,
        string eventId,
        string eventName = "LedgerEntryCreated",
        string eventVersion = "v2",
        DateTimeOffset? occurredAt = null,
        string? payload = null)
        => new(
            new EventReplaySourcePosition(
                sourceId,
                occurredAt ?? Instant("2026-06-06T12:34:56Z"),
                "Processed"),
            new EventReplayPayload(
                payload ?? ValidPayload(eventId, occurredAt ?? Instant("2026-06-06T12:34:56Z")),
                new Dictionary<string, string>
                {
                    ["event_type"] = $"{eventName}.{eventVersion}",
                    ["source"] = sourceId
                }),
            new EventReplayContract(eventName, eventVersion, "Outbox"),
            new EventReplaySubject("merchant-001", null));

    private static string ValidPayload(string eventId, DateTimeOffset occurredAt)
        => $$"""
            {
              "id": "{{eventId}}",
              "type": "CREDIT",
              "amount": "150.00",
              "currency": "BRL",
              "createdAt": "{{occurredAt:O}}",
              "merchantId": "merchant-001",
              "occurredAt": "{{occurredAt:O}}",
              "description": "Venda aprovada",
              "correlationId": "2cbdd495-586f-4565-a807-c5dc6710d237",
              "externalReference": "order-123"
            }
            """;

    private static string InvalidPayload(string eventId)
        => $$"""
            {
              "id": "{{eventId}}",
              "type": "CREDIT",
              "amount": "-150.00",
              "currency": "BRL",
              "createdAt": "2026-06-06T12:34:56.0000000Z",
              "merchantId": "merchant-001",
              "occurredAt": "2026-06-06T12:34:56.0000000Z",
              "correlationId": "2cbdd495-586f-4565-a807-c5dc6710d237"
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
}
