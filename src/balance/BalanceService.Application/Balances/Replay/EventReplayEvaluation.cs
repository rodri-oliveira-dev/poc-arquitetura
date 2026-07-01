using BalanceService.Domain.Balances;

namespace BalanceService.Application.Balances.Replay;

public sealed record EventReplayEvaluation(
    EventReplayEvaluationStatus Status,
    string? EventId,
    LedgerEntryCreatedEvent? Event,
    string? ErrorMessage)
{
    public bool IsValid => Status is EventReplayEvaluationStatus.Eligible or EventReplayEvaluationStatus.AlreadyProcessed;

    public static EventReplayEvaluation Eligible(string eventId, LedgerEntryCreatedEvent evt)
        => new(EventReplayEvaluationStatus.Eligible, eventId, evt, null);

    public static EventReplayEvaluation AlreadyProcessed(string eventId, LedgerEntryCreatedEvent evt)
        => new(EventReplayEvaluationStatus.AlreadyProcessed, eventId, evt, null);

    public static EventReplayEvaluation InvalidContract(string? eventId, string errorMessage)
        => new(EventReplayEvaluationStatus.InvalidContract, eventId, null, errorMessage);

    public static EventReplayEvaluation UnsupportedVersion(string? eventId, string errorMessage)
        => new(EventReplayEvaluationStatus.UnsupportedVersion, eventId, null, errorMessage);
}
