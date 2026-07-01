using MediatR;

namespace BalanceService.Application.Balances.Replay;

public sealed record PartialProjectionRebuildCommand(
    PartialProjectionRebuildFilter Filter,
    string Reason,
    bool Execute = false,
    int Limit = 1000) : IRequest<PartialProjectionRebuildResult>;
