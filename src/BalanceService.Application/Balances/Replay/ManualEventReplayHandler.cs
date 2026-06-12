using System.Globalization;

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

        var execution = new ReplayExecutionContext(
            Guid.NewGuid().ToString("N"),
            DryRun: false,
            command.Reason);
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
                    execution.OperationId,
                    eventId,
                    evaluation.ErrorMessage ?? "Event contract validation failed.");
                LogResult(command, execution, eventId, result);
                return result;
            }

            if (evaluation.Status == EventReplayEvaluationStatus.UnsupportedVersion)
            {
                result = ManualEventReplayResult.RejectedUnsupportedVersion(
                    execution.OperationId,
                    eventId,
                    evaluation.ErrorMessage ?? "Event contract validation failed.");
                LogResult(command, execution, eventId, result);
                return result;
            }

            if (evaluation.Status == EventReplayEvaluationStatus.AlreadyProcessed)
            {
                result = ManualEventReplayResult.SkippedAlreadyProcessed(execution.OperationId, eventId!);
                LogResult(command, execution, eventId, result);
                return result;
            }

            var evt = evaluation.Event ?? throw new InvalidOperationException("Replay evaluation returned no event.");
            var applyResult = await _sender.Send(
                new ApplyLedgerEntryCreatedCommand(evt, ToEventType(command)),
                cancellationToken);

            result = applyResult.Duplicate
                ? ManualEventReplayResult.SkippedAlreadyProcessed(execution.OperationId, eventId!)
                : ManualEventReplayResult.Replayed(execution.OperationId, eventId!);
            LogResult(command, execution, eventId, result);
            return result;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            result = eventId is null
                ? ManualEventReplayResult.RejectedInvalidContract(execution.OperationId, null, ex.Message)
                : ManualEventReplayResult.FailedProcessing(execution.OperationId, eventId, ex.Message);

            LogResult(command, execution, eventId, result, ex);
            return result;
        }
    }

    private static string ToEventType(ManualEventReplayCommand command)
        => $"{command.EventName}.{command.EventVersion}";

    private void LogResult(
        ManualEventReplayCommand command,
        ReplayExecutionContext execution,
        string? eventId,
        ManualEventReplayResult result,
        Exception? exception = null)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
            return;

        var metadata = command.Metadata is null
            ? null
            : string.Join(",", command.Metadata.Select(x => $"{x.Key}={x.Value}"));

        var eventType = $"{command.EventName}.{command.EventVersion}";
        var resultDetails = string.Create(
            CultureInfo.InvariantCulture,
            $"result={result.Result};provider={command.Provider};metadata={metadata}");

        LogManualReplayCompleted(
            _logger,
            exception,
            eventId,
            eventType,
            execution.OperationId,
            execution.Reason,
            resultDetails);
    }

    [LoggerMessage(
        EventId = 2301,
        Level = LogLevel.Information,
        Message = "Manual replay completed. eventId={EventId} eventType={EventType} replayId={ReplayId} reason={Reason} {ResultDetails}",
        SkipEnabledCheck = true)]
    private static partial void LogManualReplayCompleted(
        ILogger logger,
        Exception? exception,
        string? eventId,
        string eventType,
        string replayId,
        string reason,
        string resultDetails);
}
