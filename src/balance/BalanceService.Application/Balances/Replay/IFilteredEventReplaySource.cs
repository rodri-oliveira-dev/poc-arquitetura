namespace BalanceService.Application.Balances.Replay;

public interface IFilteredEventReplaySource
{
    Task<IReadOnlyList<EventReplaySourceCandidate>> FindAsync(
        FilteredEventReplayFilter filter,
        int limit,
        CancellationToken cancellationToken = default);
}
