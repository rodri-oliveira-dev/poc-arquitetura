using MediatR;

namespace BalanceService.Application.Balances.Replay;

public sealed record FilteredEventReplayCommand(
    FilteredEventReplayFilter Filter,
    string Reason,
    bool Execute = false,
    int Limit = 100) : IRequest<FilteredEventReplayResult>;
