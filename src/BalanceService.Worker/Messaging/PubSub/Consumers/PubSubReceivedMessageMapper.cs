using System.Globalization;

using BalanceService.Worker.Messaging.Abstractions;
using BalanceService.Worker.Messaging.PubSub.Tracing;

using Google.Cloud.PubSub.V1;

using NeutralReceivedMessage = BalanceService.Worker.Messaging.Abstractions.ReceivedMessage;

namespace BalanceService.Worker.Messaging.PubSub.Consumers;

internal static class PubSubReceivedMessageMapper
{
    private const string Provider = "pubsub";

    public static NeutralReceivedMessage Map(PubsubMessage message, string subscriptionId)
    {
        var attributes = PubSubTraceContext.ReadAttributes(message.Attributes);
        var deliveryAttempt = GetAttribute(attributes, PubSubAttributeNames.DeliveryAttempt);
        var transportMetadata = CreateTransportMetadata(message, subscriptionId, deliveryAttempt);

        return new NeutralReceivedMessage(
            message.Data.ToStringUtf8(),
            GetAttribute(attributes, PubSubAttributeNames.EventType) ?? string.Empty,
            GetAttribute(attributes, PubSubAttributeNames.EventId),
            GetAttribute(attributes, PubSubAttributeNames.CorrelationId),
            GetAttribute(attributes, PubSubAttributeNames.TraceParent),
            GetAttribute(attributes, PubSubAttributeNames.TraceState),
            GetAttribute(attributes, PubSubAttributeNames.Baggage),
            message.OrderingKey,
            attributes,
            new TransportMessageContext(
                Provider,
                subscriptionId,
                null,
                null,
                deliveryAttempt,
                transportMetadata));
    }

    private static Dictionary<string, string> CreateTransportMetadata(
        PubsubMessage message,
        string subscriptionId,
        string? deliveryAttempt)
    {
        var metadata = new Dictionary<string, string>
        {
            ["subscription_id"] = subscriptionId
        };

        AddIfPresent(metadata, "message_id", message.MessageId);
        AddIfPresent(metadata, "ordering_key", message.OrderingKey);
        AddIfPresent(metadata, "delivery_attempt", deliveryAttempt);

        if (message.PublishTime is not null)
        {
            metadata["publish_time"] = message.PublishTime
                .ToDateTimeOffset()
                .ToString("O", CultureInfo.InvariantCulture);
        }

        return metadata;
    }

    private static void AddIfPresent(Dictionary<string, string> target, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            target[name] = value;
    }

    private static string? GetAttribute(IReadOnlyDictionary<string, string> attributes, string name)
        => attributes.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
}
