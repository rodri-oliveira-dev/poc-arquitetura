using Confluent.Kafka;

using PocArquitetura.KafkaWorkerDefaults;

namespace KafkaWorkerDefaults.Tests;

public sealed class KafkaConsumerMessageHandlerTests
{
    [Fact]
    public async Task ProcessAsync_should_ignore_null_result()
    {
        var mapped = 0;
        var processed = 0;
        var committed = 0;

        await KafkaConsumerMessageHandler.ProcessAsync<object>(
            result: null,
            map: _ =>
            {
                mapped++;
                return new object();
            },
            process: (_, _) =>
            {
                processed++;
                return Task.FromResult(true);
            },
            commit: _ => committed++,
            afterCommit: null,
            CancellationToken.None);

        Assert.Equal(0, mapped);
        Assert.Equal(0, processed);
        Assert.Equal(0, committed);
    }

    [Fact]
    public async Task ProcessAsync_should_ignore_message_with_null_value()
    {
        ConsumeResult<string, string> result = CreateResult(value: null);
        var mapped = 0;
        var processed = 0;
        var committed = 0;

        await KafkaConsumerMessageHandler.ProcessAsync<object>(
            result,
            map: _ =>
            {
                mapped++;
                return new object();
            },
            process: (_, _) =>
            {
                processed++;
                return Task.FromResult(true);
            },
            commit: _ => committed++,
            afterCommit: null,
            CancellationToken.None);

        Assert.Equal(0, mapped);
        Assert.Equal(0, processed);
        Assert.Equal(0, committed);
    }

    [Fact]
    public async Task ProcessAsync_should_map_process_commit_and_invoke_callback_once_when_processor_returns_true()
    {
        ConsumeResult<string, string> result = CreateResult("payload");
        var mapped = 0;
        var processed = 0;
        var committed = 0;
        var callbacks = 0;

        await KafkaConsumerMessageHandler.ProcessAsync(
            result,
            map: received =>
            {
                mapped++;
                Assert.Same(result, received);
                return received.Message.Value;
            },
            process: (message, _) =>
            {
                processed++;
                Assert.Equal("payload", message);
                return Task.FromResult(true);
            },
            commit: received =>
            {
                committed++;
                Assert.Same(result, received);
            },
            afterCommit: () => callbacks++,
            CancellationToken.None);

        Assert.Equal(1, mapped);
        Assert.Equal(1, processed);
        Assert.Equal(1, committed);
        Assert.Equal(1, callbacks);
    }

    [Fact]
    public async Task ProcessAsync_should_not_commit_or_invoke_callback_when_processor_returns_false()
    {
        ConsumeResult<string, string> result = CreateResult("payload");
        var committed = 0;
        var callbacks = 0;

        await KafkaConsumerMessageHandler.ProcessAsync(
            result,
            map: received => received.Message.Value,
            process: (_, _) => Task.FromResult(false),
            commit: _ => committed++,
            afterCommit: () => callbacks++,
            CancellationToken.None);

        Assert.Equal(0, committed);
        Assert.Equal(0, callbacks);
    }

    [Fact]
    public async Task ProcessAsync_should_propagate_cancellation_from_processor()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        ConsumeResult<string, string> result = CreateResult("payload");

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
    public async Task ProcessAsync_should_propagate_unexpected_exception_from_processor()
    {
        ConsumeResult<string, string> result = CreateResult("payload");
        var exception = new InvalidOperationException("unexpected");

        InvalidOperationException captured = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            KafkaConsumerMessageHandler.ProcessAsync(
                result,
                map: received => received.Message.Value,
                process: (_, _) => throw exception,
                commit: _ => { },
                afterCommit: null,
                CancellationToken.None));

        Assert.Same(exception, captured);
    }

    [Theory]
    [InlineData("map")]
    [InlineData("process")]
    [InlineData("commit")]
    public async Task ProcessAsync_should_reject_required_null_delegate(string delegateName)
    {
        ConsumeResult<string, string> result = CreateResult("payload");

        Task Act() => KafkaConsumerMessageHandler.ProcessAsync<string>(
            result,
            map: delegateName == "map" ? null! : static received => received.Message.Value,
            process: delegateName == "process" ? null! : static (_, _) => Task.FromResult(true),
            commit: delegateName == "commit" ? null! : static _ => { },
            afterCommit: null,
            CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentNullException>(Act);
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
