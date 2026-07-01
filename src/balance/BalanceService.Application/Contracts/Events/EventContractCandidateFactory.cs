namespace BalanceService.Application.Contracts.Events;

public static class EventContractCandidateFactory
{
    public static EventContractValidationCandidate FromEventType(
        string? eventType,
        string payload,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        bool parsed = EventContractName.TryParse(eventType, out EventContractName? contractName);

        return new EventContractValidationCandidate(
            parsed ? contractName?.EventName : null,
            parsed ? contractName?.EventVersion : null,
            payload,
            metadata);
    }
}
