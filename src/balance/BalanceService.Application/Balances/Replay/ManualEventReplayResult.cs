namespace BalanceService.Application.Balances.Replay;

public sealed record ManualEventReplayResult(
    ManualEventReplayStatus Result,
    string ReplayId,
    string? EventId,
    string? ErrorMessage)
{
    public static ManualEventReplayResult Replayed(string replayId, string eventId)
        => new(ManualEventReplayStatus.Replayed, replayId, eventId, null);

    public static ManualEventReplayResult SkippedAlreadyProcessed(string replayId, string eventId)
        => new(ManualEventReplayStatus.SkippedAlreadyProcessed, replayId, eventId, null);

    public static ManualEventReplayResult RejectedInvalidContract(
        string replayId,
        string? eventId,
        string errorMessage)
        => new(ManualEventReplayStatus.RejectedInvalidContract, replayId, eventId, errorMessage);

    public static ManualEventReplayResult RejectedUnsupportedVersion(
        string replayId,
        string? eventId,
        string errorMessage)
        => new(ManualEventReplayStatus.RejectedUnsupportedVersion, replayId, eventId, errorMessage);

    public static ManualEventReplayResult FailedProcessing(
        string replayId,
        string eventId,
        string errorMessage)
        => new(ManualEventReplayStatus.FailedProcessing, replayId, eventId, errorMessage);
}
