using System.Text.Json;
using System.Text.Json.Serialization;

using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Balances.Commands;
using BalanceService.Application.Contracts.Events;
using BalanceService.Domain.Balances;

using MediatR;

using Microsoft.Extensions.Logging;

namespace BalanceService.Application.Balances.Replay;

public sealed partial class ManualEventReplayHandler : IRequestHandler<ManualEventReplayCommand, ManualEventReplayResult>
{
    private const string SupportedEventName = "LedgerEntryCreated";
    private const string LegacyV1CurrencyFallback = "BRL";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly IEventContractValidator _contractValidator;
    private readonly IProcessedEventRepository _processedEventRepository;
    private readonly ISender _sender;
    private readonly ILogger<ManualEventReplayHandler> _logger;

    public ManualEventReplayHandler(
        IEventContractValidator contractValidator,
        IProcessedEventRepository processedEventRepository,
        ISender sender,
        ILogger<ManualEventReplayHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(contractValidator);
        ArgumentNullException.ThrowIfNull(processedEventRepository);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(logger);

        _contractValidator = contractValidator;
        _processedEventRepository = processedEventRepository;
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
            var contractResult = ValidateContract(command);
            if (!contractResult.IsValid)
            {
                result = ToRejectedResult(replayId, null, contractResult);
                LogResult(command, replayId, null, result);
                return result;
            }

            if (!string.Equals(command.EventName, SupportedEventName, StringComparison.Ordinal))
            {
                result = ManualEventReplayResult.RejectedInvalidContract(
                    replayId,
                    null,
                    $"Manual replay supports only '{SupportedEventName}'.");
                LogResult(command, replayId, null, result);
                return result;
            }

            LedgerEntryCreatedEvent evt;
            try
            {
                evt = JsonSerializer.Deserialize<LedgerEntryCreatedEvent>(command.Payload, JsonOptions)
                    ?? throw new JsonException("Payload deserialized to null.");
            }
            catch (JsonException ex)
            {
                result = ManualEventReplayResult.RejectedInvalidContract(replayId, null, ex.Message);
                LogResult(command, replayId, null, result);
                return result;
            }

            evt = NormalizeEvent(evt, command.EventVersion);
            eventId = evt.Id;

            if (await _processedEventRepository.ExistsAsync(eventId, cancellationToken))
            {
                result = ManualEventReplayResult.SkippedAlreadyProcessed(replayId, eventId);
                LogResult(command, replayId, eventId, result);
                return result;
            }

            var applyResult = await _sender.Send(
                new ApplyLedgerEntryCreatedCommand(evt, ToEventType(command)),
                cancellationToken);

            result = applyResult.Duplicate
                ? ManualEventReplayResult.SkippedAlreadyProcessed(replayId, eventId)
                : ManualEventReplayResult.Replayed(replayId, eventId);
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

    private EventContractValidationResult ValidateContract(ManualEventReplayCommand command)
    {
        var candidate = new EventContractValidationCandidate(
            command.EventName,
            command.EventVersion,
            command.Payload,
            command.Metadata);

        return _contractValidator.Validate(candidate);
    }

    private static ManualEventReplayResult ToRejectedResult(
        string replayId,
        string? eventId,
        EventContractValidationResult contractResult)
    {
        var errorMessage = contractResult.ErrorMessage ?? "Event contract validation failed.";
        return contractResult.ErrorCode == EventContractValidationErrorCode.UnsupportedVersion
            ? ManualEventReplayResult.RejectedUnsupportedVersion(replayId, eventId, errorMessage)
            : ManualEventReplayResult.RejectedInvalidContract(replayId, eventId, errorMessage);
    }

    private static LedgerEntryCreatedEvent NormalizeEvent(LedgerEntryCreatedEvent evt, string eventVersion)
    {
        if (string.Equals(eventVersion, "v1", StringComparison.Ordinal))
            return evt with { Currency = LegacyV1CurrencyFallback };

        return evt;
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
