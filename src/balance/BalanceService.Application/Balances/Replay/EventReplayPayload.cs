namespace BalanceService.Application.Balances.Replay;

public sealed record EventReplayPayload(
    string Payload,
    IReadOnlyDictionary<string, string>? Metadata);
