using Confluent.Kafka;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using TransferService.Worker.Messaging;
using TransferService.Worker.Options;
using TransferService.Worker.Tests.Support;

using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace TransferService.Worker.Tests.Messaging;

public sealed class KafkaTransferenciaOutboxPublisherTests
{
    [Fact]
    public async Task PublishAsync_should_publish_valid_message_with_topic_and_saga_key()
    {
        var producer = CreateCapturingProducer();
        using var sut = CreatePublisher(producer.Mock.Object);
        var outbox = TransferenciaWorkerTestData.CreateOutboxMessage();

        await sut.PublishAsync(outbox, "transfer.transferencia.compensada", CancellationToken.None);

        Assert.Equal("transfer.transferencia.compensada", producer.Topic);
        Assert.NotNull(producer.Message);
        var capturedMessage = producer.Message;
        Assert.Equal(outbox.AggregateId.ToString(), capturedMessage.Key);
        Assert.Equal(outbox.Payload, capturedMessage.Value);
        AssertHeader(capturedMessage.Headers, "event_type", outbox.EventType);
        AssertHeader(capturedMessage.Headers, "aggregate_id", outbox.AggregateId.ToString());
        AssertHeader(capturedMessage.Headers, "correlation_id", outbox.CorrelationId!);
    }

    [Fact]
    public async Task PublishDlqAsync_should_publish_payload_and_reason_to_dlq_topic()
    {
        var producer = CreateCapturingProducer();
        using var sut = CreatePublisher(producer.Mock.Object);
        var outbox = TransferenciaWorkerTestData.CreateOutboxMessage(payload: /*lang=json,strict*/ "{\"status\":\"failed\"}");

        await sut.PublishDlqAsync(outbox, "erro definitivo", "transfer.transferencia.dlq", CancellationToken.None);

        Assert.Equal("transfer.transferencia.dlq", producer.Topic);
        Assert.NotNull(producer.Message);
        var capturedMessage = producer.Message;
        Assert.Equal(outbox.AggregateId.ToString(), capturedMessage.Key);
        Assert.Contains("\"reason\": \"erro definitivo\"", capturedMessage.Value, StringComparison.Ordinal);
        Assert.Contains("\"payload\": \"{\\u0022status\\u0022:\\u0022failed\\u0022}\"", capturedMessage.Value, StringComparison.Ordinal);
        AssertHeader(capturedMessage.Headers, "dlq_reason", "erro definitivo");
    }

    [Fact]
    public async Task PublishAsync_should_wrap_producer_error()
    {
        var producer = new Mock<IProducer<string, string>>();
        producer
            .Setup(x => x.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProduceException<string, string>(
                new Error(ErrorCode.Local_MsgTimedOut, "timeout"),
                new DeliveryResult<string, string>()));

        using var sut = CreatePublisher(producer.Object);
        var outbox = TransferenciaWorkerTestData.CreateOutboxMessage();

        var exception = await Assert.ThrowsAsync<TransferenciaKafkaPublishException>(
            () => sut.PublishAsync(outbox, outbox.Topic, CancellationToken.None));

        Assert.False(exception.IsTransient);
        Assert.IsType<ProduceException<string, string>>(exception.InnerException);
    }

    [Fact]
    public async Task PublishAsync_should_treat_timeout_as_transient_error()
    {
        var producer = new Mock<IProducer<string, string>>();
        producer
            .Setup(x => x.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("timeout"));

        using var sut = CreatePublisher(producer.Object);
        var outbox = TransferenciaWorkerTestData.CreateOutboxMessage();

        var exception = await Assert.ThrowsAsync<TransferenciaKafkaPublishException>(
            () => sut.PublishAsync(outbox, outbox.Topic, CancellationToken.None));

        Assert.True(exception.IsTransient);
        Assert.IsType<TimeoutException>(exception.InnerException);
    }

    [Fact]
    public async Task PublishDlqAsync_should_validate_public_arguments()
    {
        using var sut = CreatePublisher(Mock.Of<IProducer<string, string>>());
        var outbox = TransferenciaWorkerTestData.CreateOutboxMessage();

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.PublishAsync(null!, outbox.Topic, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.PublishAsync(outbox, null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.PublishDlqAsync(null!, "reason", outbox.Topic, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.PublishDlqAsync(outbox, null!, outbox.Topic, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.PublishDlqAsync(outbox, "reason", null!, CancellationToken.None));
    }

    [Fact]
    public void Constructor_should_validate_dependencies()
    {
        var options = OptionsFactory.Create(new TransferWorkerOptions());
        var logger = NullLogger<KafkaTransferenciaOutboxPublisher>.Instance;

        static void CreateWithNullOptions(ILogger<KafkaTransferenciaOutboxPublisher> logger)
        {
            _ = new KafkaTransferenciaOutboxPublisher(null!, logger, Mock.Of<IProducer<string, string>>());
        }

        static void CreateWithNullLogger(IOptions<TransferWorkerOptions> options)
        {
            _ = new KafkaTransferenciaOutboxPublisher(options, null!, Mock.Of<IProducer<string, string>>());
        }

        Assert.Throws<ArgumentNullException>(() => CreateWithNullOptions(logger));
        Assert.Throws<ArgumentNullException>(() => CreateWithNullLogger(options));
    }

    private static KafkaTransferenciaOutboxPublisher CreatePublisher(IProducer<string, string> producer)
        => new(
            OptionsFactory.Create(new TransferWorkerOptions
            {
                Kafka =
                {
                    BootstrapServers = "localhost:9092",
                    ClientId = "transfer-service-worker-tests"
                }
            }),
            NullLogger<KafkaTransferenciaOutboxPublisher>.Instance,
            producer);

    private static void AssertHeader(Headers headers, string key, string expectedValue)
    {
        var header = headers.Single(x => x.Key == key);
        Assert.Equal(expectedValue, System.Text.Encoding.UTF8.GetString(header.GetValueBytes()));
    }

    private static CapturingProducer CreateCapturingProducer()
    {
        var producer = new CapturingProducer();
        producer.Mock
            .Setup(x => x.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((topic, message, _) =>
            {
                producer.Topic = topic;
                producer.Message = message;
            })
            .ReturnsAsync(new DeliveryResult<string, string>());

        return producer;
    }

    private sealed class CapturingProducer
    {
        public Mock<IProducer<string, string>> Mock { get; } = new();

        public string? Topic
        {
            get; set;
        }

        public Message<string, string>? Message
        {
            get; set;
        }
    }
}
