using BalanceService.Application.Contracts.Events;

namespace BalanceService.Application.Balances.Replay;

public sealed record EventReplaySourceCandidate(
    EventReplaySourcePosition SourcePosition,
    EventReplayPayload ReplayPayload,
    EventReplayContract Contract,
    EventReplaySubject Subject)
{
    public string SourceId => SourcePosition.SourceId;
    public string Payload => ReplayPayload.Payload;
    public string EventName => Contract.EventName;
    public string EventVersion => Contract.EventVersion;
    public string? Provider => Contract.Provider;
    public DateTimeOffset OccurredAt => SourcePosition.OccurredAt;
    public string? MerchantId => Subject.MerchantId;
    public string? AccountId => Subject.AccountId;
    public string? Status => SourcePosition.Status;
    public IReadOnlyDictionary<string, string>? Metadata => ReplayPayload.Metadata;

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
            new EventReplaySourcePosition(sourceId, occurredAt, status),
            new EventReplayPayload(payload, metadata),
            new EventReplayContract(contractName.EventName, contractName.EventVersion, provider),
            new EventReplaySubject(merchantId, accountId));
    }
}
