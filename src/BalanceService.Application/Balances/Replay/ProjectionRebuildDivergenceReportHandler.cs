using System.Globalization;

using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Domain.Balances;
using BalanceService.Domain.Exceptions;

using MediatR;

using Microsoft.Extensions.Logging;

namespace BalanceService.Application.Balances.Replay;

public sealed partial class ProjectionRebuildDivergenceReportHandler
    : IRequestHandler<ProjectionRebuildDivergenceReportCommand, ProjectionRebuildDivergenceReportResult>
{
    private const string SupportedEventName = "LedgerEntryCreated";
    private const string ProcessedOutboxStatus = "Processed";
    private const string MultipleAccountIds = "__multiple_account_ids__";

    private readonly IFilteredEventReplaySource _source;
    private readonly EventReplayMessageEvaluator _evaluator;
    private readonly IDailyBalanceReadRepository _dailyBalanceReadRepository;
    private readonly IClock _clock;
    private readonly ILogger<ProjectionRebuildDivergenceReportHandler> _logger;

    public ProjectionRebuildDivergenceReportHandler(
        IFilteredEventReplaySource source,
        EventReplayMessageEvaluator evaluator,
        IDailyBalanceReadRepository dailyBalanceReadRepository,
        IClock clock,
        ILogger<ProjectionRebuildDivergenceReportHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(dailyBalanceReadRepository);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _source = source;
        _evaluator = evaluator;
        _dailyBalanceReadRepository = dailyBalanceReadRepository;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ProjectionRebuildDivergenceReportResult> Handle(
        ProjectionRebuildDivergenceReportCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Filter);
        ValidateFilter(command.Filter);

        var limit = Math.Clamp(command.Limit, 1, 1000);
        var execution = new ReplayExecutionContext(
            Guid.NewGuid().ToString("N"),
            DryRun: true,
            command.Reason);
        var filterDescription = Describe(command.Filter);
        var replayFilter = new FilteredEventReplayFilter(
            SupportedEventName,
            command.Filter.EventVersion,
            command.Filter.OccurredFrom,
            command.Filter.OccurredUntil,
            command.Filter.MerchantId,
            AccountId: null,
            ProcessedOutboxStatus);

        var sourceCandidates = await _source.FindAsync(replayFilter, limit, cancellationToken);
        var candidates = sourceCandidates
            .Where(candidate => Matches(replayFilter, candidate))
            .OrderBy(candidate => candidate.OccurredAt)
            .ThenBy(candidate => candidate.SourceId, StringComparer.Ordinal)
            .Take(limit)
            .ToList();

        var evaluated = await EvaluateCandidatesAsync(candidates, command.Filter, cancellationToken);
        var eventItems = evaluated.Select(x => x.Item).ToList();
        var validEvents = evaluated
            .Where(x => x.Event is not null && x.Item.Status == ProjectionRebuildEventItemStatus.Eligible)
            .OrderBy(x => x.Event!.OccurredAt.UtcDateTime)
            .ThenBy(x => x.Event!.Id, StringComparer.Ordinal)
            .ToList();

        var currentBalances = await _dailyBalanceReadRepository.ListByPeriodAsync(
            command.Filter.MerchantId,
            DateOnly.FromDateTime(command.Filter.OccurredFrom.Date),
            DateOnly.FromDateTime(command.Filter.OccurredUntil.Date),
            cancellationToken);

        var rebuiltBalances = RebuildInMemory(validEvents);
        var items = Compare(currentBalances, rebuiltBalances, eventItems);
        var totalInvalid = eventItems.Count(x =>
            x.Status is ProjectionRebuildEventItemStatus.RejectedInvalidContract or
                ProjectionRebuildEventItemStatus.RejectedUnsupportedVersion);
        var totalDuplicates = eventItems.Count(x => x.Status == ProjectionRebuildEventItemStatus.DuplicateInBatch);
        var result = new ProjectionRebuildDivergenceReportResult(
            execution.OperationId,
            false,
            filterDescription,
            new ProjectionRebuildDivergenceSummary(
                candidates.Count,
                candidates.Count - totalInvalid,
                totalInvalid,
                totalDuplicates,
                items.Count,
                items.Any(x => x.Difference != 0m)),
            items,
            eventItems);

        LogProjectionRebuildDivergenceReportCompleted(
            _logger,
            execution,
            filterDescription,
            result.Summary);

        return result;
    }

    private async Task<IReadOnlyList<EvaluatedCandidate>> EvaluateCandidatesAsync(
        List<EventReplaySourceCandidate> candidates,
        PartialProjectionRebuildFilter filter,
        CancellationToken cancellationToken)
    {
        var seenEventIds = new HashSet<string>(StringComparer.Ordinal);
        var evaluated = new List<EvaluatedCandidate>(candidates.Count);

        foreach (var candidate in candidates)
        {
            var evaluation = await _evaluator.EvaluateAsync(
                candidate.Payload,
                candidate.EventName,
                candidate.EventVersion,
                candidate.Metadata,
                cancellationToken,
                checkAlreadyProcessed: false);

            var item = ToItem(candidate, evaluation, ToStatus(evaluation.Status));
            if (evaluation.Event is not null && !EventMatchesFilter(evaluation.Event, filter))
            {
                evaluated.Add(new EvaluatedCandidate(
                    item with
                    {
                        Status = ProjectionRebuildEventItemStatus.RejectedInvalidContract,
                        ErrorMessage = "Event payload does not match rebuild filter."
                    },
                    null,
                    candidate.AccountId));
                continue;
            }

            if (evaluation.EventId is not null &&
                evaluation.Event is not null &&
                item.Status == ProjectionRebuildEventItemStatus.Eligible &&
                !seenEventIds.Add(evaluation.EventId))
            {
                evaluated.Add(new EvaluatedCandidate(
                    item with { Status = ProjectionRebuildEventItemStatus.DuplicateInBatch },
                    null,
                    candidate.AccountId));
                continue;
            }

            evaluated.Add(new EvaluatedCandidate(item, evaluation.Event, candidate.AccountId));
        }

        return evaluated;
    }

    private Dictionary<ProjectionKey, RebuiltBalance> RebuildInMemory(
        IReadOnlyList<EvaluatedCandidate> eligible)
    {
        var rebuilt = new Dictionary<ProjectionKey, RebuiltBalance>();
        var now = _clock.UtcNow;

        foreach (var candidate in eligible)
        {
            var evt = candidate.Event!;
            var currency = evt.Currency ?? throw new InvalidOperationException("Event currency is required.");
            var key = new ProjectionKey(
                evt.MerchantId,
                DateOnly.FromDateTime(evt.OccurredAt.Date),
                currency.Trim().ToUpperInvariant());

            if (!rebuilt.TryGetValue(key, out var balance))
            {
                balance = new RebuiltBalance(
                    new DailyBalance(key.MerchantId, key.Date, key.Currency, now),
                    AccountId: null,
                    EventsAnalyzed: 0);
            }

            try
            {
                balance.DailyBalance.Apply(NormalizeEvent(evt), now);
            }
            catch (DomainException ex)
            {
                throw new InvalidOperationException("Valid event failed while rebuilding projection report.", ex);
            }

            rebuilt[key] = balance with
            {
                AccountId = CommonAccountId(balance.AccountId, candidate.AccountId),
                EventsAnalyzed = balance.EventsAnalyzed + 1
            };
        }

        return rebuilt;
    }

    private static List<ProjectionRebuildDivergenceItem> Compare(
        IReadOnlyList<Queries.Models.DailyBalanceReadModel> currentBalances,
        Dictionary<ProjectionKey, RebuiltBalance> rebuiltBalances,
        IReadOnlyList<ProjectionRebuildEventItemResult> eventItems)
    {
        var current = currentBalances.ToDictionary(
            x => new ProjectionKey(x.MerchantId, x.Date, x.Currency.Trim().ToUpperInvariant()),
            x => x);
        var keys = current.Keys
            .Concat(rebuiltBalances.Keys)
            .Distinct()
            .OrderBy(x => x.MerchantId, StringComparer.Ordinal)
            .ThenBy(x => x.Date)
            .ThenBy(x => x.Currency, StringComparer.Ordinal)
            .ToList();
        var totalInvalid = eventItems.Count(x =>
            x.Status is ProjectionRebuildEventItemStatus.RejectedInvalidContract or
                ProjectionRebuildEventItemStatus.RejectedUnsupportedVersion);
        var totalDuplicates = eventItems.Count(x => x.Status == ProjectionRebuildEventItemStatus.DuplicateInBatch);
        var items = new List<ProjectionRebuildDivergenceItem>(keys.Count);

        foreach (var key in keys)
        {
            current.TryGetValue(key, out var currentBalance);
            rebuiltBalances.TryGetValue(key, out var rebuilt);

            var currentNet = currentBalance?.NetBalance ?? 0m;
            var rebuiltNet = rebuilt?.DailyBalance.NetBalance ?? 0m;
            items.Add(new ProjectionRebuildDivergenceItem(
                new ProjectionRebuildDivergenceIdentity(
                    rebuilt?.AccountId == MultipleAccountIds ? null : rebuilt?.AccountId,
                    key.MerchantId,
                    key.Date,
                    key.Currency),
                new ProjectionRebuildDivergenceValues(
                    currentNet,
                    rebuiltNet,
                    rebuiltNet - currentNet),
                new ProjectionRebuildDivergenceCounters(
                    rebuilt?.EventsAnalyzed ?? 0,
                    totalInvalid,
                    totalDuplicates)));
        }

        return items;
    }

    private static void ValidateFilter(PartialProjectionRebuildFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.MerchantId))
            throw new ArgumentException("MerchantId is required.", nameof(filter));

        if (filter.OccurredFrom >= filter.OccurredUntil)
            throw new ArgumentException("OccurredFrom must be earlier than OccurredUntil.", nameof(filter));

        if (string.IsNullOrWhiteSpace(filter.EventVersion))
            throw new ArgumentException("EventVersion is required.", nameof(filter));
    }

    private static bool EventMatchesFilter(LedgerEntryCreatedEvent evt, PartialProjectionRebuildFilter filter)
        => string.Equals(evt.MerchantId, filter.MerchantId, StringComparison.Ordinal) &&
            evt.OccurredAt >= filter.OccurredFrom &&
            evt.OccurredAt <= filter.OccurredUntil;

    private static bool Matches(FilteredEventReplayFilter filter, EventReplaySourceCandidate candidate)
    {
        if (!string.Equals(filter.EventName, candidate.EventName, StringComparison.Ordinal))
            return false;

        if (!string.Equals(filter.EventVersion, candidate.EventVersion, StringComparison.Ordinal))
            return false;

        if (!string.Equals(filter.MerchantId, candidate.MerchantId, StringComparison.Ordinal))
            return false;

        if (!string.Equals(filter.Status, candidate.Status, StringComparison.Ordinal))
            return false;

        if (candidate.OccurredAt < filter.OccurredFrom || candidate.OccurredAt > filter.OccurredUntil)
            return false;

        return true;
    }

    private static LedgerEntryCreatedEvent NormalizeEvent(LedgerEntryCreatedEvent evt)
    {
        var currency = evt.Currency ?? throw new InvalidOperationException("Event currency is required.");

        return evt with
        {
            OccurredAt = evt.OccurredAt.ToUniversalTime(),
            CreatedAt = evt.CreatedAt.ToUniversalTime(),
            Currency = currency.Trim().ToUpperInvariant()
        };
    }

    private static ProjectionRebuildEventItemStatus ToStatus(EventReplayEvaluationStatus status)
        => status switch
        {
            EventReplayEvaluationStatus.Eligible => ProjectionRebuildEventItemStatus.Eligible,
            EventReplayEvaluationStatus.AlreadyProcessed => ProjectionRebuildEventItemStatus.Eligible,
            EventReplayEvaluationStatus.InvalidContract => ProjectionRebuildEventItemStatus.RejectedInvalidContract,
            EventReplayEvaluationStatus.UnsupportedVersion => ProjectionRebuildEventItemStatus.RejectedUnsupportedVersion,
            _ => ProjectionRebuildEventItemStatus.RejectedInvalidContract
        };

    private static ProjectionRebuildEventItemResult ToItem(
        EventReplaySourceCandidate candidate,
        EventReplayEvaluation evaluation,
        ProjectionRebuildEventItemStatus status)
        => new(
            candidate.SourceId,
            evaluation.EventId,
            candidate.EventName,
            candidate.EventVersion,
            candidate.AccountId,
            status,
            evaluation.ErrorMessage);

    private static string? CommonAccountId(string? current, string? next)
    {
        if (string.IsNullOrWhiteSpace(next))
            return current;

        if (string.IsNullOrWhiteSpace(current))
            return next;

        if (string.Equals(current, MultipleAccountIds, StringComparison.Ordinal))
            return current;

        return string.Equals(current, next, StringComparison.Ordinal) ? current : MultipleAccountIds;
    }

    private static string Describe(PartialProjectionRebuildFilter filter)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"merchantId={filter.MerchantId};occurredFrom={filter.OccurredFrom:O};occurredUntil={filter.OccurredUntil:O};eventVersion={filter.EventVersion}");

    private sealed record EvaluatedCandidate(
        ProjectionRebuildEventItemResult Item,
        LedgerEntryCreatedEvent? Event,
        string? AccountId);

    private sealed record ProjectionKey(
        string MerchantId,
        DateOnly Date,
        string Currency);

    private sealed record RebuiltBalance(
        DailyBalance DailyBalance,
        string? AccountId,
        int EventsAnalyzed);

    [LoggerMessage(
        EventId = 2304,
        Level = LogLevel.Information,
        Message = "Projection rebuild divergence report completed. execution={Execution} filter={FilterDescription} summary={Summary}")]
    private static partial void LogProjectionRebuildDivergenceReportCompleted(
        ILogger logger,
        ReplayExecutionContext execution,
        string filterDescription,
        ProjectionRebuildDivergenceSummary summary);
}
