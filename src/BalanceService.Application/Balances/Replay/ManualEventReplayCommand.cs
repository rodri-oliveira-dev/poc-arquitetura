using MediatR;

namespace BalanceService.Application.Balances.Replay;

public sealed record ManualEventReplayCommand(
    string Payload,
    string EventName,
    string EventVersion,
    string? Provider,
    IReadOnlyDictionary<string, string>? Metadata,
    string Reason) : IRequest<ManualEventReplayResult>;
