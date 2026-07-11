using System.Text.Json;
using System.Text.Json.Serialization;

using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Contracts.Events;
using BalanceService.Application.IntegrationEvents;

namespace BalanceService.Application.Balances.Replay;

public sealed class EventReplayMessageEvaluator
{
    private const string SupportedEventName = "LedgerEntryCreated";
    private const string LegacyV1CurrencyFallback = "BRL";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly IEventContractValidator _contractValidator;
    private readonly IProcessedEventRepository _processedEventRepository;

    public EventReplayMessageEvaluator(
        IEventContractValidator contractValidator,
        IProcessedEventRepository processedEventRepository)
    {
        ArgumentNullException.ThrowIfNull(contractValidator);
        ArgumentNullException.ThrowIfNull(processedEventRepository);

        _contractValidator = contractValidator;
        _processedEventRepository = processedEventRepository;
    }

    public async Task<EventReplayEvaluation> EvaluateAsync(
        string payload,
        string eventName,
        string eventVersion,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken,
        bool checkAlreadyProcessed = true)
    {
        var contractResult = ValidateContract(payload, eventName, eventVersion, metadata);
        if (!contractResult.IsValid)
            return ToRejectedEvaluation(null, contractResult);

        if (!string.Equals(eventName, SupportedEventName, StringComparison.Ordinal))
        {
            return EventReplayEvaluation.InvalidContract(
                null,
                $"Manual replay supports only '{SupportedEventName}'.");
        }

        LedgerEntryCreatedIntegrationEvent evt;
        try
        {
            evt = JsonSerializer.Deserialize<LedgerEntryCreatedIntegrationEvent>(payload, JsonOptions)
                ?? throw new JsonException("Payload deserialized to null.");
        }
        catch (JsonException ex)
        {
            return EventReplayEvaluation.InvalidContract(null, ex.Message);
        }

        evt = NormalizeEvent(evt, eventVersion);

        if (checkAlreadyProcessed &&
            await _processedEventRepository.ExistsAsync(evt.Id, cancellationToken))
        {
            return EventReplayEvaluation.AlreadyProcessed(evt.Id, evt);
        }

        return EventReplayEvaluation.Eligible(evt.Id, evt);
    }

    private EventContractValidationResult ValidateContract(
        string payload,
        string eventName,
        string eventVersion,
        IReadOnlyDictionary<string, string>? metadata)
    {
        var candidate = new EventContractValidationCandidate(eventName, eventVersion, payload, metadata);
        return _contractValidator.Validate(candidate);
    }

    private static EventReplayEvaluation ToRejectedEvaluation(
        string? eventId,
        EventContractValidationResult contractResult)
    {
        var errorMessage = contractResult.ErrorMessage ?? "Event contract validation failed.";
        return contractResult.ErrorCode == EventContractValidationErrorCode.UnsupportedVersion
            ? EventReplayEvaluation.UnsupportedVersion(eventId, errorMessage)
            : EventReplayEvaluation.InvalidContract(eventId, errorMessage);
    }

    private static LedgerEntryCreatedIntegrationEvent NormalizeEvent(LedgerEntryCreatedIntegrationEvent evt, string eventVersion)
    {
        if (string.Equals(eventVersion, "v1", StringComparison.Ordinal))
            return evt with
            {
                Currency = LegacyV1CurrencyFallback
            };

        return evt;
    }
}
