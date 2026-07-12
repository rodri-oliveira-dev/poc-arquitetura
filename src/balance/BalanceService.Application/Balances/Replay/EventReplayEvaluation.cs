using BalanceService.Application.IntegrationEvents;

namespace BalanceService.Application.Balances.Replay;

public sealed record EventReplayEvaluation(
    EventReplayEvaluationStatus Status,
    string? EventId,
    LedgerEntryCreatedIntegrationEvent? Event,
    string? ErrorMessage)
{
    public bool IsValid => Status is EventReplayEvaluationStatus.Eligible or EventReplayEvaluationStatus.AlreadyProcessed;

    public static EventReplayEvaluation Eligible(string eventId, LedgerEntryCreatedIntegrationEvent evt)
        => new(EventReplayEvaluationStatus.Eligible, eventId, evt, null);

    public static EventReplayEvaluation AlreadyProcessed(string eventId, LedgerEntryCreatedIntegrationEvent evt)
        => new(EventReplayEvaluationStatus.AlreadyProcessed, eventId, evt, null);

    public static EventReplayEvaluation InvalidContract(string? eventId, string errorMessage)
        => new(EventReplayEvaluationStatus.InvalidContract, eventId, null, errorMessage);

    public static EventReplayEvaluation UnsupportedVersion(string? eventId, string errorMessage)
        => new(EventReplayEvaluationStatus.UnsupportedVersion, eventId, null, errorMessage);
}
