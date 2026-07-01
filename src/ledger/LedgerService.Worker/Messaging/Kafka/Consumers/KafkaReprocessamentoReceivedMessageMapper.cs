using System.Globalization;
using System.Text;

using Confluent.Kafka;

using LedgerService.Worker.Messaging.Abstractions;
using LedgerService.Worker.Messaging.Kafka.Tracing;

namespace LedgerService.Worker.Messaging.Kafka.Consumers;

internal static class KafkaReprocessamentoReceivedMessageMapper
{
    private const string Provider = "kafka";

    public static ReceivedMessage Map(ConsumeResult<string, string> result)
    {
        var attributes = ReadHeaders(result.Message.Headers);
        var partition = result.Partition.Value.ToString(CultureInfo.InvariantCulture);
        var offset = result.Offset.Value.ToString(CultureInfo.InvariantCulture);
        var transportMetadata = new Dictionary<string, string>
        {
            ["topic"] = result.Topic,
            ["partition"] = partition,
            ["offset"] = offset
        };

        if (!string.IsNullOrWhiteSpace(result.Message.Key))
            transportMetadata["key"] = result.Message.Key;

        return new ReceivedMessage(
            result.Message.Value,
            GetAttribute(attributes, KafkaHeaderNames.EventType) ?? string.Empty,
            GetAttribute(attributes, KafkaHeaderNames.EventId),
            GetAttribute(attributes, KafkaHeaderNames.CorrelationId),
            GetAttribute(attributes, KafkaHeaderNames.TraceParent),
            GetAttribute(attributes, KafkaHeaderNames.TraceState),
            GetAttribute(attributes, KafkaHeaderNames.Baggage),
            result.Message.Key,
            attributes,
            new TransportMessageContext(
                Provider,
                result.Topic,
                partition,
                offset,
                null,
                transportMetadata));
    }

    private static IReadOnlyDictionary<string, string> ReadHeaders(Headers? headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headers is null)
            return result;

        foreach (var header in headers)
            result[header.Key] = Encoding.UTF8.GetString(header.GetValueBytes());

        return result;
    }

    private static string? GetAttribute(IReadOnlyDictionary<string, string> attributes, string name)
        => attributes.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
}
