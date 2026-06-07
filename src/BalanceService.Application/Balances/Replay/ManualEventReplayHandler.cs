using BalanceService.Application.Balances.Commands;

using MediatR;

using Microsoft.Extensions.Logging;

namespace BalanceService.Application.Balances.Replay;

public sealed partial class ManualEventReplayHandler : IRequestHandler<ManualEventReplayCommand, ManualEventReplayResult>
{
    private readonly EventReplayMessageEvaluator _evaluator;
    private readonly ISender _sender;
    private readonly ILogger<ManualEventReplayHandler> _logger;

    public ManualEventReplayHandler(
        EventReplayMessageEvaluator evaluator,
        ISender sender,
        ILogger<ManualEventReplayHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(logger);

        _evaluator = evaluator;
        _sender = sender;
        _logger = logger;
    }

    public async Task<ManualEventReplayResult> Handle(
        ManualEventReplayCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var replayId = Guid.NewGuid().ToString("N");
        string? eventId = null;

        ManualEventReplayResult result;
        try
        {
            var evaluation = await _evaluator.EvaluateAsync(
                command.Payload,
                command.EventName,
                command.EventVersion,
                command.Metadata,
                cancellationToken);
            eventId = evaluation.EventId;

            if (evaluation.Status == EventReplayEvaluationStatus.InvalidContract)
            {
                result = ManualEventReplayResult.RejectedInvalidContract(
                    replayId,
                    eventId,
                    evaluation.ErrorMessage ?? "Event contract validation failed.");
                LogResult(command, replayId, eventId, result);
                return result;
            }

            if (evaluation.Status == EventReplayEvaluationStatus.UnsupportedVersion)
            {
                result = ManualEventReplayResult.RejectedUnsupportedVersion(
                    replayId,
                    eventId,
                    evaluation.ErrorMessage ?? "Event contract validation failed.");
                LogResult(command, replayId, eventId, result);
                return result;
            }

            if (evaluation.Status == EventReplayEvaluationStatus.AlreadyProcessed)
            {
                result = ManualEventReplayResult.SkippedAlreadyProcessed(replayId, eventId!);
                LogResult(command, replayId, eventId, result);
                return result;
            }

            var evt = evaluation.Event ?? throw new InvalidOperationException("Replay evaluation returned no event.");
            var applyResult = await _sender.Send(
                new ApplyLedgerEntryCreatedCommand(evt, ToEventType(command)),
                cancellationToken);

            result = applyResult.Duplicate
                ? ManualEventReplayResult.SkippedAlreadyProcessed(replayId, eventId!)
                : ManualEventReplayResult.Replayed(replayId, eventId!);
            LogResult(command, replayId, eventId, result);
            return result;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            result = eventId is null
                ? ManualEventReplayResult.RejectedInvalidContract(replayId, null, ex.Message)
                : ManualEventReplayResult.FailedProcessing(replayId, eventId, ex.Message);

            LogResult(command, replayId, eventId, result, ex);
            return result;
        }
    }

    private static string ToEventType(ManualEventReplayCommand command)
        => $"{command.EventName}.{command.EventVersion}";

    private void LogResult(
        ManualEventReplayCommand command,
        string replayId,
        string? eventId,
        ManualEventReplayResult result,
        Exception? exception = null)
    {
        var metadata = command.Metadata is null
            ? null
            : string.Join(",", command.Metadata.Select(x => $"{x.Key}={x.Value}"));

        LogManualReplayCompleted(
            _logger,
            exception,
            eventId,
            command.EventName,
            command.EventVersion,
            replayId,
            command.Reason,
            result.Result,
            command.Provider,
            metadata);
    }

    [LoggerMessage(
        EventId = 2301,
        Level = LogLevel.Information,
        Message = "Manual replay completed. eventId={EventId} eventName={EventName} eventVersion={EventVersion} replayId={ReplayId} reason={Reason} result={Result} provider={Provider} metadata={Metadata}")]
    private static partial void LogManualReplayCompleted(
        ILogger logger,
        Exception? exception,
        string? eventId,
        string eventName,
        string eventVersion,
        string replayId,
        string reason,
        ManualEventReplayStatus result,
        string? provider,
        string? metadata);
}
