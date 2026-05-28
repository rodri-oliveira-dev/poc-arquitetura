using System.Globalization;

using BalanceService.Worker.Messaging.Abstractions;
using BalanceService.Worker.Messaging.Kafka.Tracing;

using Confluent.Kafka;

namespace BalanceService.Worker.Messaging.Kafka.Consumers;

internal static class KafkaReceivedMessageMapper
{
    private const string Provider = "kafka";

    public static ReceivedMessage Map(ConsumeResult<string, string> result)
    {
        var attributes = KafkaTraceContext.ReadHeaders(result.Message.Headers);

        var transportMetadata = new Dictionary<string, string>
        {
            ["topic"] = result.Topic,
            ["partition"] = result.Partition.Value.ToString(CultureInfo.InvariantCulture),
            ["offset"] = result.Offset.Value.ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(result.Message.Key))
            transportMetadata["key"] = result.Message.Key;

        return new ReceivedMessage(
            result.Message.Value,
            GetAttribute(attributes, MessageAttributeNames.EventType) ?? string.Empty,
            GetAttribute(attributes, MessageAttributeNames.EventId),
            GetAttribute(attributes, MessageAttributeNames.CorrelationId),
            GetAttribute(attributes, MessageAttributeNames.TraceParent),
            GetAttribute(attributes, MessageAttributeNames.TraceState),
            GetAttribute(attributes, MessageAttributeNames.Baggage),
            result.Message.Key,
            attributes,
            new TransportMessageContext(
                Provider,
                result.Topic,
                result.Partition.Value.ToString(CultureInfo.InvariantCulture),
                result.Offset.Value.ToString(CultureInfo.InvariantCulture),
                null,
                transportMetadata));
    }

    private static string? GetAttribute(IReadOnlyDictionary<string, string> attributes, string name)
        => attributes.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
}
