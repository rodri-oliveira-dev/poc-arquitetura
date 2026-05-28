using System.Text;

using BalanceService.Worker.Messaging.Kafka.Consumers;
using BalanceService.Worker.Messaging.Kafka.Contracts;
using BalanceService.Worker.Messaging.Kafka.Tracing;

using Confluent.Kafka;

namespace BalanceService.Worker.Tests.Messaging.Kafka.Consumers;

public sealed class KafkaReceivedMessageMapperTests
{
    [Fact]
    public void Map_should_copy_payload_headers_key_and_transport_context()
    {
        var result = new ConsumeResult<string, string>
        {
            Topic = "ledger.ledgerentry.created",
            Partition = new Partition(3),
            Offset = new Offset(99),
            Message = new Message<string, string>
            {
                Key = "merchant-1",
                Value = "{\"id\":\"lan_1\"}",
                Headers = new Headers
                {
                    { KafkaHeaderNames.EventType, Encoding.UTF8.GetBytes(LedgerEntryCreatedV1Contract.EventType) },
                    { KafkaHeaderNames.EventId, Encoding.UTF8.GetBytes("evt-1") },
                    { KafkaHeaderNames.CorrelationId, Encoding.UTF8.GetBytes("corr-1") },
                    { KafkaHeaderNames.TraceParent, Encoding.UTF8.GetBytes("traceparent") },
                    { KafkaHeaderNames.TraceState, Encoding.UTF8.GetBytes("tracestate") },
                    { KafkaHeaderNames.Baggage, Encoding.UTF8.GetBytes("tenant=poc") }
                }
            }
        };

        var message = KafkaReceivedMessageMapper.Map(result);

        Assert.Equal("{\"id\":\"lan_1\"}", message.Payload);
        Assert.Equal(LedgerEntryCreatedV1Contract.EventType, message.EventType);
        Assert.Equal("evt-1", message.EventId);
        Assert.Equal("corr-1", message.CorrelationId);
        Assert.Equal("traceparent", message.TraceParent);
        Assert.Equal("tracestate", message.TraceState);
        Assert.Equal("tenant=poc", message.Baggage);
        Assert.Equal("merchant-1", message.OrderingKey);
        Assert.Equal("kafka", message.Transport.Provider);
        Assert.Equal("ledger.ledgerentry.created", message.Transport.Source);
        Assert.Equal("3", message.Transport.Partition);
        Assert.Equal("99", message.Transport.Offset);
        Assert.Null(message.Transport.DeliveryAttempt);
        Assert.Equal("ledger.ledgerentry.created", message.Transport.Metadata["topic"]);
        Assert.Equal("3", message.Transport.Metadata["partition"]);
        Assert.Equal("99", message.Transport.Metadata["offset"]);
        Assert.Equal("merchant-1", message.Transport.Metadata["key"]);
    }
}
