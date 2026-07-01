using BalanceService.Application.Contracts.Events;
using BalanceService.Worker.Messaging.PubSub.Tracing;

namespace BalanceService.Worker.Messaging.PubSub.Consumers;

internal static class PubSubEventContractCandidateMapper
{
    public static EventContractValidationCandidate Map(
        string payload,
        IReadOnlyDictionary<string, string> attributes,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        attributes.TryGetValue(PubSubAttributeNames.EventType, out string? eventType);
        return EventContractCandidateFactory.FromEventType(eventType, payload, metadata ?? attributes);
    }
}
