using System.Text;

using Confluent.Kafka;

using LedgerService.Application.Lancamentos.Events;
using LedgerService.Worker.Messaging.Kafka.Consumers;
using LedgerService.Worker.Messaging.Kafka.Tracing;

namespace LedgerService.Worker.Tests.Messaging.Kafka.Consumers;

public sealed class KafkaReprocessamentoReceivedMessageMapperTests
{
    [Fact]
    public void Map_should_copy_payload_headers_key_and_transport_context()
    {
        var result = new ConsumeResult<string, string>
        {
            Topic = "ledger.lancamentos.reprocessamento.solicitado",
            Partition = new Partition(3),
            Offset = new Offset(99),
            Message = new Message<string, string>
            {
                Key = "merchant-1",
                Value = "{\"reprocessamentoId\":\"17f05de3-09ec-44ef-a971-0114f214116e\"}",
                Headers = new Headers
                {
                    { KafkaHeaderNames.EventType, Encoding.UTF8.GetBytes(ReprocessamentoLancamentosSolicitadoV1.EventType) },
                    { KafkaHeaderNames.EventId, Encoding.UTF8.GetBytes("evt-1") },
                    { KafkaHeaderNames.CorrelationId, Encoding.UTF8.GetBytes("corr-1") },
                    { KafkaHeaderNames.TraceParent, Encoding.UTF8.GetBytes("traceparent") },
                    { KafkaHeaderNames.TraceState, Encoding.UTF8.GetBytes("tracestate") },
                    { KafkaHeaderNames.Baggage, Encoding.UTF8.GetBytes("tenant=poc") }
                }
            }
        };

        var message = KafkaReprocessamentoReceivedMessageMapper.Map(result);

        Assert.Equal(result.Message.Value, message.Payload);
        Assert.Equal(ReprocessamentoLancamentosSolicitadoV1.EventType, message.EventType);
        Assert.Equal("evt-1", message.EventId);
        Assert.Equal("corr-1", message.CorrelationId);
        Assert.Equal("traceparent", message.TraceParent);
        Assert.Equal("tracestate", message.TraceState);
        Assert.Equal("tenant=poc", message.Baggage);
        Assert.Equal("merchant-1", message.OrderingKey);
        Assert.Equal("kafka", message.Transport.Provider);
        Assert.Equal(result.Topic, message.Transport.Source);
        Assert.Equal("3", message.Transport.Partition);
        Assert.Equal("99", message.Transport.Offset);
        Assert.Null(message.Transport.DeliveryAttempt);
        Assert.Equal(result.Topic, message.Transport.Metadata["topic"]);
        Assert.Equal("3", message.Transport.Metadata["partition"]);
        Assert.Equal("99", message.Transport.Metadata["offset"]);
        Assert.Equal("merchant-1", message.Transport.Metadata["key"]);
    }

    [Fact]
    public void Map_should_use_empty_event_type_when_header_is_missing()
    {
        var result = new ConsumeResult<string, string>
        {
            Topic = "ledger.lancamentos.reprocessamento.solicitado",
            Message = new Message<string, string>
            {
                Value = "{}"
            }
        };

        var message = KafkaReprocessamentoReceivedMessageMapper.Map(result);

        Assert.Empty(message.EventType);
        Assert.Empty(message.Attributes);
        Assert.DoesNotContain("key", message.Transport.Metadata);
    }
}
