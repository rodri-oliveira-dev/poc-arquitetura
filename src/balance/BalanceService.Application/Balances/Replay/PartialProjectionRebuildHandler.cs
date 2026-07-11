using System.Globalization;

using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Idempotency;
using BalanceService.Application.IntegrationEvents;
using BalanceService.Domain.Balances;

using MediatR;

using Microsoft.Extensions.Logging;

namespace BalanceService.Application.Balances.Replay;

public sealed partial class PartialProjectionRebuildHandler
    : IRequestHandler<PartialProjectionRebuildCommand, PartialProjectionRebuildResult>
{
    private const string SupportedEventName = "LedgerEntryCreated";
    private const string ProcessedOutboxStatus = "Processed";

    private readonly IFilteredEventReplaySource _source;
    private readonly EventReplayMessageEvaluator _evaluator;
    private readonly IDailyBalanceRepository _dailyBalanceRepository;
    private readonly IProcessedEventRepository _processedEventRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<PartialProjectionRebuildHandler> _logger;

    public PartialProjectionRebuildHandler(
        IFilteredEventReplaySource source,
        EventReplayMessageEvaluator evaluator,
        IDailyBalanceRepository dailyBalanceRepository,
        IProcessedEventRepository processedEventRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<PartialProjectionRebuildHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(dailyBalanceRepository);
        ArgumentNullException.ThrowIfNull(processedEventRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _source = source;
        _evaluator = evaluator;
        _dailyBalanceRepository = dailyBalanceRepository;
        _processedEventRepository = processedEventRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<PartialProjectionRebuildResult> Handle(
        PartialProjectionRebuildCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Filter);
        ValidateFilter(command.Filter);

        var limit = Math.Clamp(command.Limit, 1, 1000);
        var execution = new ReplayExecutionContext(
            Guid.NewGuid().ToString("N"),
            DryRun: !command.Execute,
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
        var items = evaluated.Select(x => x.Item).ToList();
        var uniqueEligible = evaluated
            .Where(x => x.Event is not null && x.Item.Status == PartialProjectionRebuildItemStatus.Eligible)
            .OrderBy(x => x.Event!.OccurredAt.UtcDateTime)
            .ThenBy(x => x.Event!.Id, StringComparer.Ordinal)
            .ToList();

        var totalInvalid = items.Count(x =>
            x.Status is PartialProjectionRebuildItemStatus.RejectedInvalidContract or
                PartialProjectionRebuildItemStatus.RejectedUnsupportedVersion);
        var totalDuplicates = items.Count(x => x.Status == PartialProjectionRebuildItemStatus.DuplicateInBatch);
        var totalEligible = uniqueEligible.Count;
        var totalRejected = totalInvalid;
        var totalValid = candidates.Count - totalInvalid;
        var totalRebuilt = 0;
        var totalDailyBalancesDeleted = 0;
        var totalProcessedEventsDeleted = 0;
        var mutated = false;

        if (!execution.DryRun && totalRejected == 0 && totalEligible > 0)
        {
            var rebuildResult = await RebuildAsync(uniqueEligible, cancellationToken);
            totalRebuilt = rebuildResult.TotalRebuilt;
            totalDailyBalancesDeleted = rebuildResult.TotalDailyBalancesDeleted;
            totalProcessedEventsDeleted = rebuildResult.TotalProcessedEventsDeleted;
            mutated = rebuildResult.Mutated;

            items = [.. items
                .Select(item => item.Status == PartialProjectionRebuildItemStatus.Eligible
                    ? item with
                    {
                        Status = rebuildResult.RebuiltEventIds.Contains(item.EventId ?? string.Empty)
                        ? PartialProjectionRebuildItemStatus.Rebuilt
                        : PartialProjectionRebuildItemStatus.SkippedConcurrentDuplicate
                    }
                    : item)];
        }

        var result = new PartialProjectionRebuildResult(
            execution.OperationId,
            execution.DryRun,
            mutated,
            filterDescription,
            new ProjectionRebuildEvaluationSummary(
                candidates.Count,
                totalValid,
                totalInvalid,
                totalDuplicates,
                totalEligible,
                totalRejected),
            new ProjectionRebuildMutationSummary(
                totalRebuilt,
                totalDailyBalancesDeleted,
                totalProcessedEventsDeleted),
            items);

        LogPartialProjectionRebuildCompleted(
            _logger,
            execution,
            mutated,
            filterDescription,
            result.EvaluationSummary,
            result.MutationSummary);

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
                        Status = PartialProjectionRebuildItemStatus.RejectedInvalidContract,
                        ErrorMessage = "Event payload does not match rebuild filter."
                    },
                    null));
                continue;
            }

            if (evaluation.EventId is not null &&
                evaluation.Event is not null &&
                item.Status == PartialProjectionRebuildItemStatus.Eligible &&
                !seenEventIds.Add(evaluation.EventId))
            {
                evaluated.Add(new EvaluatedCandidate(
                    item with
                    {
                        Status = PartialProjectionRebuildItemStatus.DuplicateInBatch
                    },
                    null));
                continue;
            }

            evaluated.Add(new EvaluatedCandidate(item, evaluation.Event));
        }

        return evaluated;
    }

    private async Task<RebuildPersistenceResult> RebuildAsync(
        IReadOnlyList<EvaluatedCandidate> eligible,
        CancellationToken cancellationToken)
    {
        var events = eligible.Select(x => x.Event!).ToArray();
        var eventIds = events.Select(x => x.Id).Distinct(StringComparer.Ordinal).ToArray();
        var dates = events
            .Select(x => DateOnly.FromDateTime(x.OccurredAt.Date))
            .Distinct()
            .Order()
            .ToArray();
        var currencies = events
            .Select(x => x.Currency ?? throw new InvalidOperationException("Event currency is required."))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var merchantId = events[0].MerchantId;
        var now = _clock.UtcNow;

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        foreach (var date in dates)
        {
            foreach (var currency in currencies)
            {
                await _dailyBalanceRepository.LockByMerchantDateAndCurrencyAsync(
                    merchantId,
                    date,
                    currency,
                    cancellationToken);
            }
        }

        var deletedDailyBalances = await _dailyBalanceRepository.DeleteByMerchantAndDateRangeAsync(
            merchantId,
            dates[0],
            dates[^1],
            cancellationToken);
        var deletedProcessedEvents = await _processedEventRepository.DeleteByEventIdsAsync(
            eventIds,
            cancellationToken);

        var rebuiltEventIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var evt in events)
        {
            var normalizedEvent = NormalizeEvent(evt);
            var movement = LedgerEntryCreatedIntegrationEventMapper.ToBalanceMovement(normalizedEvent);
            var inserted = await _processedEventRepository.TryInsertAsync(
                new ProcessedEvent(normalizedEvent.Id, normalizedEvent.MerchantId, movement.OccurredAt, now),
                cancellationToken);

            if (!inserted)
                continue;

            var date = movement.Date;
            var currency = movement.Currency.Code;
            var dailyBalance = await _dailyBalanceRepository.GetByMerchantDateAndCurrencyAsync(
                evt.MerchantId,
                date,
                currency,
                cancellationToken);

            if (dailyBalance is null)
            {
                dailyBalance = new DailyBalance(evt.MerchantId, date, currency, now);
                await _dailyBalanceRepository.AddAsync(dailyBalance, cancellationToken);
            }

            dailyBalance.Apply(movement, now);
            rebuiltEventIds.Add(evt.Id);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RebuildPersistenceResult(
            Mutated: true,
            TotalRebuilt: rebuiltEventIds.Count,
            TotalDailyBalancesDeleted: deletedDailyBalances,
            TotalProcessedEventsDeleted: deletedProcessedEvents,
            RebuiltEventIds: rebuiltEventIds);
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

    private static bool EventMatchesFilter(LedgerEntryCreatedIntegrationEvent evt, PartialProjectionRebuildFilter filter)
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

        return !(candidate.OccurredAt < filter.OccurredFrom) && !(candidate.OccurredAt > filter.OccurredUntil);
    }

    private static LedgerEntryCreatedIntegrationEvent NormalizeEvent(LedgerEntryCreatedIntegrationEvent evt)
    {
        var currency = evt.Currency ?? throw new InvalidOperationException("Event currency is required.");

        return evt with
        {
            OccurredAt = evt.OccurredAt.ToUniversalTime(),
            CreatedAt = evt.CreatedAt.ToUniversalTime(),
            Currency = currency.Trim().ToUpperInvariant()
        };
    }

    private static PartialProjectionRebuildItemStatus ToStatus(EventReplayEvaluationStatus status)
        => status switch
        {
            EventReplayEvaluationStatus.Eligible => PartialProjectionRebuildItemStatus.Eligible,
            EventReplayEvaluationStatus.AlreadyProcessed => PartialProjectionRebuildItemStatus.Eligible,
            EventReplayEvaluationStatus.InvalidContract => PartialProjectionRebuildItemStatus.RejectedInvalidContract,
            EventReplayEvaluationStatus.UnsupportedVersion => PartialProjectionRebuildItemStatus.RejectedUnsupportedVersion,
            _ => PartialProjectionRebuildItemStatus.RejectedInvalidContract
        };

    private static PartialProjectionRebuildItemResult ToItem(
        EventReplaySourceCandidate candidate,
        EventReplayEvaluation evaluation,
        PartialProjectionRebuildItemStatus status)
        => new(
            candidate.SourceId,
            evaluation.EventId,
            candidate.EventName,
            candidate.EventVersion,
            status,
            evaluation.ErrorMessage);

    private static string Describe(PartialProjectionRebuildFilter filter)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"merchantId={filter.MerchantId};occurredFrom={filter.OccurredFrom:O};occurredUntil={filter.OccurredUntil:O};eventVersion={filter.EventVersion}");

    private sealed record EvaluatedCandidate(
        PartialProjectionRebuildItemResult Item,
        LedgerEntryCreatedIntegrationEvent? Event);

    private sealed record RebuildPersistenceResult(
        bool Mutated,
        int TotalRebuilt,
        int TotalDailyBalancesDeleted,
        int TotalProcessedEventsDeleted,
        IReadOnlySet<string> RebuiltEventIds);

    [LoggerMessage(
        EventId = 2303,
        Level = LogLevel.Information,
        Message = "Partial projection rebuild completed. execution={Execution} mutated={Mutated} filter={FilterDescription} evaluation={EvaluationSummary} mutation={MutationSummary}")]
    private static partial void LogPartialProjectionRebuildCompleted(
        ILogger logger,
        ReplayExecutionContext execution,
        bool mutated,
        string filterDescription,
        ProjectionRebuildEvaluationSummary evaluationSummary,
        ProjectionRebuildMutationSummary mutationSummary);
}
