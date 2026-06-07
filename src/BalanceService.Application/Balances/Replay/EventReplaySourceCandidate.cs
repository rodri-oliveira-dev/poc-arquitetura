using BalanceService.Application.Contracts.Events;

namespace BalanceService.Application.Balances.Replay;

public sealed record EventReplaySourceCandidate(
    string SourceId,
    string Payload,
    string EventName,
    string EventVersion,
    string? Provider,
    DateTimeOffset OccurredAt,
    string? MerchantId,
    string? AccountId,
    string? Status,
    IReadOnlyDictionary<string, string>? Metadata)
{
    public static EventReplaySourceCandidate FromEventType(
        string sourceId,
        string payload,
        string eventType,
        string? provider,
        DateTimeOffset occurredAt,
        string? merchantId,
        string? accountId,
        string? status,
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (!EventContractName.TryParse(eventType, out EventContractName? contractName) || contractName is null)
            throw new ArgumentException("Event type must contain event name and version.", nameof(eventType));

        return new EventReplaySourceCandidate(
            sourceId,
            payload,
            contractName.EventName,
            contractName.EventVersion,
            provider,
            occurredAt,
            merchantId,
            accountId,
            status,
            metadata);
    }
}
