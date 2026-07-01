using MediatR;

namespace BalanceService.Application.Balances.Replay;

public sealed record ProjectionRebuildDivergenceReportCommand(
    PartialProjectionRebuildFilter Filter,
    string Reason,
    int Limit = 1000)
    : IRequest<ProjectionRebuildDivergenceReportResult>;
