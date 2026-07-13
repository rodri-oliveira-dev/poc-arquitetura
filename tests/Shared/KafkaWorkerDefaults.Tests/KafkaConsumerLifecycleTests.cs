using Confluent.Kafka;

using PocArquitetura.KafkaWorkerDefaults;

namespace KafkaWorkerDefaults.Tests;

public sealed class KafkaConsumerLifecycleTests
{
    [Fact]
    public void Close_should_execute_delegate()
    {
        var closed = false;

        KafkaConsumerLifecycle.Close(() => closed = true);

        Assert.True(closed);
    }

    [Fact]
    public void Close_should_reject_null_delegate()
    {
        Assert.Throws<ArgumentNullException>(() => KafkaConsumerLifecycle.Close(null!));
    }

    [Fact]
    public void Close_should_not_throw_when_close_fails_with_kafka_exception()
    {
        var exception = new KafkaException(new Error(ErrorCode.Local_Application, "shutdown"));

        Exception? captured = Record.Exception(() => KafkaConsumerLifecycle.Close(() => throw exception));

        Assert.Null(captured);
    }

    [Fact]
    public void Close_should_propagate_unexpected_exception()
    {
        var exception = new InvalidOperationException("unexpected");

        InvalidOperationException captured = Assert.Throws<InvalidOperationException>(
            () => KafkaConsumerLifecycle.Close(() => throw exception));

        Assert.Same(exception, captured);
    }
}
