using MediatR;

using Microsoft.Extensions.Logging;

namespace BalanceService.Application.Balances.Replay;

public sealed partial class FilteredEventReplayHandler
    : IRequestHandler<FilteredEventReplayCommand, FilteredEventReplayResult>
{
    private readonly IFilteredEventReplaySource _source;
    private readonly EventReplayMessageEvaluator _evaluator;
    private readonly ISender _sender;
    private readonly ILogger<FilteredEventReplayHandler> _logger;

    public FilteredEventReplayHandler(
        IFilteredEventReplaySource source,
        EventReplayMessageEvaluator evaluator,
        ISender sender,
        ILogger<FilteredEventReplayHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(logger);

        _source = source;
        _evaluator = evaluator;
        _sender = sender;
        _logger = logger;
    }

    public async Task<FilteredEventReplayResult> Handle(
        FilteredEventReplayCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Filter);

        var limit = Math.Clamp(command.Limit, 1, 1000);
        var execution = new ReplayExecutionContext(
            Guid.NewGuid().ToString("N"),
            DryRun: !command.Execute,
            command.Reason);

        var sourceCandidates = await _source.FindAsync(command.Filter, limit, cancellationToken);
        var candidates = sourceCandidates
            .Where(candidate => Matches(command.Filter, candidate))
            .OrderBy(candidate => candidate.OccurredAt)
            .Take(limit)
            .ToList();

        var seenEligibleEventIds = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<FilteredEventReplayItemResult>(candidates.Count);
        var totalValid = 0;
        var totalInvalid = 0;
        var totalAlreadyProcessed = 0;
        var totalEligible = 0;
        var totalRejected = 0;
        var totalReplayed = 0;

        foreach (var candidate in candidates)
        {
            var evaluation = await _evaluator.EvaluateAsync(
                candidate.Payload,
                candidate.EventName,
                candidate.EventVersion,
                candidate.Metadata,
                cancellationToken);

            if (evaluation.IsValid)
                totalValid++;
            else
                totalInvalid++;

            if (evaluation.Status == EventReplayEvaluationStatus.InvalidContract)
            {
                totalRejected++;
                items.Add(ToItem(candidate, evaluation, FilteredEventReplayItemStatus.RejectedInvalidContract));
                continue;
            }

            if (evaluation.Status == EventReplayEvaluationStatus.UnsupportedVersion)
            {
                totalRejected++;
                items.Add(ToItem(candidate, evaluation, FilteredEventReplayItemStatus.RejectedUnsupportedVersion));
                continue;
            }

            if (evaluation.Status == EventReplayEvaluationStatus.AlreadyProcessed ||
                !seenEligibleEventIds.Add(evaluation.EventId!))
            {
                totalAlreadyProcessed++;
                items.Add(ToItem(candidate, evaluation, FilteredEventReplayItemStatus.AlreadyProcessed));
                continue;
            }

            totalEligible++;

            if (execution.DryRun)
            {
                items.Add(ToItem(candidate, evaluation, FilteredEventReplayItemStatus.Eligible));
                continue;
            }

            var replayResult = await _sender.Send(
                new ManualEventReplayCommand(
                    candidate.Payload,
                    candidate.EventName,
                    candidate.EventVersion,
                    candidate.Provider,
                    candidate.Metadata,
                    command.Reason),
                cancellationToken);

            switch (replayResult.Result)
            {
                case ManualEventReplayStatus.Replayed:
                    totalReplayed++;
                    items.Add(ToItem(candidate, replayResult, FilteredEventReplayItemStatus.Replayed));
                    break;

                case ManualEventReplayStatus.SkippedAlreadyProcessed:
                    totalAlreadyProcessed++;
                    items.Add(ToItem(candidate, replayResult, FilteredEventReplayItemStatus.AlreadyProcessed));
                    break;

                case ManualEventReplayStatus.RejectedUnsupportedVersion:
                    totalRejected++;
                    items.Add(ToItem(candidate, replayResult, FilteredEventReplayItemStatus.RejectedUnsupportedVersion));
                    break;

                case ManualEventReplayStatus.RejectedInvalidContract:
                    totalRejected++;
                    items.Add(ToItem(candidate, replayResult, FilteredEventReplayItemStatus.RejectedInvalidContract));
                    break;

                case ManualEventReplayStatus.FailedProcessing:
                    totalRejected++;
                    items.Add(ToItem(candidate, replayResult, FilteredEventReplayItemStatus.FailedProcessing));
                    break;
            }
        }

        var result = new FilteredEventReplayResult(
            execution.OperationId,
            execution.DryRun,
            new FilteredEventReplaySummary(
                candidates.Count,
                totalValid,
                totalInvalid,
                totalAlreadyProcessed,
                totalEligible,
                totalRejected,
                totalReplayed),
            items);

        LogFilteredReplayCompleted(
            _logger,
            execution,
            result.Summary);

        return result;
    }

    private static bool Matches(FilteredEventReplayFilter filter, EventReplaySourceCandidate candidate)
    {
        if (!MatchesText(filter.EventName, candidate.EventName))
            return false;

        if (!MatchesText(filter.EventVersion, candidate.EventVersion))
            return false;

        if (filter.OccurredFrom is not null && candidate.OccurredAt < filter.OccurredFrom)
            return false;

        if (filter.OccurredUntil is not null && candidate.OccurredAt > filter.OccurredUntil)
            return false;

        if (!MatchesText(filter.MerchantId, candidate.MerchantId))
            return false;

        if (!MatchesText(filter.AccountId, candidate.AccountId))
            return false;

        if (!MatchesText(filter.Status, candidate.Status))
            return false;

        return true;
    }

    private static bool MatchesText(string? expected, string? actual)
        => string.IsNullOrWhiteSpace(expected) ||
            string.Equals(expected, actual, StringComparison.Ordinal);

    private static FilteredEventReplayItemResult ToItem(
        EventReplaySourceCandidate candidate,
        EventReplayEvaluation evaluation,
        FilteredEventReplayItemStatus status)
        => new(
            candidate.SourceId,
            evaluation.EventId,
            candidate.EventName,
            candidate.EventVersion,
            status,
            evaluation.ErrorMessage);

    private static FilteredEventReplayItemResult ToItem(
        EventReplaySourceCandidate candidate,
        ManualEventReplayResult replayResult,
        FilteredEventReplayItemStatus status)
        => new(
            candidate.SourceId,
            replayResult.EventId,
            candidate.EventName,
            candidate.EventVersion,
            status,
            replayResult.ErrorMessage);

    [LoggerMessage(
        EventId = 2302,
        Level = LogLevel.Information,
        Message = "Filtered replay completed. execution={Execution} summary={Summary}")]
    private static partial void LogFilteredReplayCompleted(
        ILogger logger,
        ReplayExecutionContext execution,
        FilteredEventReplaySummary summary);
}
