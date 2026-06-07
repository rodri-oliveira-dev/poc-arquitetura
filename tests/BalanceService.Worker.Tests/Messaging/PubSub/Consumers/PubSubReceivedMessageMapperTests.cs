using System.Globalization;

using BalanceService.Worker.Messaging.PubSub.Consumers;
using BalanceService.Worker.Messaging.PubSub.Tracing;

using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace BalanceService.Worker.Tests.Messaging.PubSub.Consumers;

public sealed class PubSubReceivedMessageMapperTests
{
    private const string SubscriptionId = "ledger-events-balance";

    [Fact]
    public void Map_should_decode_utf8_payload()
    {
        var pubSubMessage = new PubsubMessage
        {
            Data = ByteString.CopyFromUtf8("{\"description\":\"lançamento\"}")
        };

        var message = PubSubReceivedMessageMapper.Map(pubSubMessage, SubscriptionId);

        Assert.Equal("{\"description\":\"lançamento\"}", message.Payload);
    }

    [Fact]
    public void Map_should_copy_required_attributes()
    {
        var pubSubMessage = CreateMessage();
        pubSubMessage.Attributes.Add(PubSubAttributeNames.EventType, "LedgerEntryCreated.v1");
        pubSubMessage.Attributes.Add(PubSubAttributeNames.EventId, "evt-1");
        pubSubMessage.Attributes.Add(PubSubAttributeNames.CorrelationId, "corr-1");

        var message = PubSubReceivedMessageMapper.Map(pubSubMessage, SubscriptionId);

        Assert.Equal("LedgerEntryCreated.v1", message.EventType);
        Assert.Equal("evt-1", message.EventId);
        Assert.Equal("corr-1", message.CorrelationId);
        Assert.Equal("evt-1", message.Attributes[PubSubAttributeNames.EventId]);
    }

    [Fact]
    public void Map_should_copy_trace_context()
    {
        var pubSubMessage = CreateMessage();
        pubSubMessage.Attributes.Add(PubSubAttributeNames.TraceParent, "traceparent");
        pubSubMessage.Attributes.Add(PubSubAttributeNames.TraceState, "tracestate");
        pubSubMessage.Attributes.Add(PubSubAttributeNames.Baggage, "tenant=poc");

        var message = PubSubReceivedMessageMapper.Map(pubSubMessage, SubscriptionId);

        Assert.Equal("traceparent", message.TraceParent);
        Assert.Equal("tracestate", message.TraceState);
        Assert.Equal("tenant=poc", message.Baggage);
    }

    [Fact]
    public void Map_should_copy_ordering_key()
    {
        var pubSubMessage = CreateMessage();
        pubSubMessage.OrderingKey = "merchant-1";

        var message = PubSubReceivedMessageMapper.Map(pubSubMessage, SubscriptionId);

        Assert.Equal("merchant-1", message.OrderingKey);
        Assert.Equal("merchant-1", message.Transport.Metadata["ordering_key"]);
    }

    [Fact]
    public void Map_should_copy_delivery_attempt_attribute()
    {
        var pubSubMessage = CreateMessage();
        pubSubMessage.Attributes.Add(PubSubAttributeNames.DeliveryAttempt, "3");

        var message = PubSubReceivedMessageMapper.Map(pubSubMessage, SubscriptionId);

        Assert.Equal("3", message.Transport.DeliveryAttempt);
        Assert.Equal("3", message.Transport.Metadata["delivery_attempt"]);
    }

    [Fact]
    public void Map_should_create_pubsub_metadata_without_kafka_coordinates()
    {
        var publishedAt = DateTimeOffset.Parse(
            "2026-06-01T12:34:56.0000000+00:00",
            CultureInfo.InvariantCulture);
        var pubSubMessage = CreateMessage();
        pubSubMessage.MessageId = "message-1";
        pubSubMessage.PublishTime = Timestamp.FromDateTimeOffset(publishedAt);
        pubSubMessage.OrderingKey = "merchant-1";

        var message = PubSubReceivedMessageMapper.Map(pubSubMessage, SubscriptionId);

        Assert.Equal("pubsub", message.Transport.Provider);
        Assert.Equal(SubscriptionId, message.Transport.Source);
        Assert.Null(message.Transport.Partition);
        Assert.Null(message.Transport.Offset);
        Assert.Equal("message-1", message.Transport.Metadata["message_id"]);
        Assert.Equal(publishedAt.ToString("O", CultureInfo.InvariantCulture), message.Transport.Metadata["publish_time"]);
        Assert.Equal("merchant-1", message.Transport.Metadata["ordering_key"]);
        Assert.Equal(SubscriptionId, message.Transport.Metadata["subscription_id"]);
        Assert.DoesNotContain("partition", message.Transport.Metadata);
        Assert.DoesNotContain("offset", message.Transport.Metadata);
    }

    private static PubsubMessage CreateMessage()
        => new()
        {
            Data = ByteString.CopyFromUtf8("{}")
        };
}
