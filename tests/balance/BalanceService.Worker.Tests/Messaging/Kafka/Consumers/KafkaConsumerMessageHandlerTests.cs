using Confluent.Kafka;

using PocArquitetura.KafkaWorkerDefaults;

namespace BalanceService.Worker.Tests.Messaging.Kafka.Consumers;

public sealed class KafkaConsumerMessageHandlerTests
{
    [Fact]
    public async Task ProcessAsync_should_ignore_null_result()
    {
        var mapped = false;
        var processed = false;
        var committed = false;

        await KafkaConsumerMessageHandler.ProcessAsync<object>(
            result: null,
            map: _ =>
            {
                mapped = true;
                return new object();
            },
            process: (_, _) =>
            {
                processed = true;
                return Task.FromResult(true);
            },
            commit: _ => committed = true,
            afterCommit: null,
            CancellationToken.None);

        Assert.False(mapped);
        Assert.False(processed);
        Assert.False(committed);
    }

    [Fact]
    public async Task ProcessAsync_should_ignore_message_with_null_value()
    {
        var result = CreateResult(value: null);
        var mapped = false;
        var processed = false;
        var committed = false;

        await KafkaConsumerMessageHandler.ProcessAsync<object>(
            result,
            map: _ =>
            {
                mapped = true;
                return new object();
            },
            process: (_, _) =>
            {
                processed = true;
                return Task.FromResult(true);
            },
            commit: _ => committed = true,
            afterCommit: null,
            CancellationToken.None);

        Assert.False(mapped);
        Assert.False(processed);
        Assert.False(committed);
    }

    [Fact]
    public async Task ProcessAsync_should_map_process_and_commit_when_processor_returns_true()
    {
        var result = CreateResult("payload");
        var mapped = false;
        var processed = false;
        var committed = false;
        var afterCommit = false;

        await KafkaConsumerMessageHandler.ProcessAsync(
            result,
            map: received =>
            {
                mapped = ReferenceEquals(result, received);
                return received.Message.Value;
            },
            process: (message, _) =>
            {
                processed = message == "payload";
                return Task.FromResult(true);
            },
            commit: received => committed = ReferenceEquals(result, received),
            afterCommit: () => afterCommit = true,
            CancellationToken.None);

        Assert.True(mapped);
        Assert.True(processed);
        Assert.True(committed);
        Assert.True(afterCommit);
    }

    [Fact]
    public async Task ProcessAsync_should_not_commit_when_processor_returns_false()
    {
        var result = CreateResult("payload");
        var committed = false;
        var afterCommit = false;

        await KafkaConsumerMessageHandler.ProcessAsync(
            result,
            map: received => received.Message.Value,
            process: (_, _) => Task.FromResult(false),
            commit: _ => committed = true,
            afterCommit: () => afterCommit = true,
            CancellationToken.None);

        Assert.False(committed);
        Assert.False(afterCommit);
    }

    [Fact]
    public async Task ProcessAsync_should_propagate_cancellation_from_processor()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var result = CreateResult("payload");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            KafkaConsumerMessageHandler.ProcessAsync(
                result,
                map: received => received.Message.Value,
                process: (_, ct) => Task.FromCanceled<bool>(ct),
                commit: _ => { },
                afterCommit: null,
                cts.Token));
    }

    [Fact]
    public void KafkaConsumerLifecycle_should_not_throw_when_close_fails_with_kafka_exception()
    {
        var error = new Error(ErrorCode.Local_Application, "shutdown");
        var exception = new KafkaException(error);

        var ex = Record.Exception(() => KafkaConsumerLifecycle.Close(() => throw exception));

        Assert.Null(ex);
    }

    private static ConsumeResult<string, string> CreateResult(string? value)
        => new()
        {
            Topic = "ledger.ledgerentry.created",
            Partition = new Partition(0),
            Offset = new Offset(42),
            Message = new Message<string, string>
            {
                Key = "key",
                Value = value!,
                Headers = new Headers()
            }
        };
}
