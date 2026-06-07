using BalanceService.Application.Contracts.Events;
using BalanceService.Worker.Messaging.Abstractions;

namespace BalanceService.Worker.Messaging.Kafka.Consumers;

internal static class KafkaEventContractCandidateMapper
{
    public static EventContractValidationCandidate Map(
        string payload,
        IReadOnlyDictionary<string, string> headers,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        headers.TryGetValue(MessageAttributeNames.EventType, out string? eventType);
        return EventContractCandidateFactory.FromEventType(eventType, payload, metadata ?? headers);
    }
}
