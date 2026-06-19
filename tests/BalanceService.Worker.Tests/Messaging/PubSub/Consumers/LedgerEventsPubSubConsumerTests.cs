using BalanceService.Worker.Messaging.PubSub.Configuration;
using BalanceService.Worker.Messaging.PubSub.Consumers;
using BalanceService.Worker.Observability;

using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NeutralReceivedMessage = BalanceService.Worker.Messaging.Abstractions.ReceivedMessage;

namespace BalanceService.Worker.Tests.Messaging.PubSub.Consumers;

public sealed class LedgerEventsPubSubConsumerTests
{
    private static readonly MessagingMetrics _metrics = new($"BalanceService.Worker.Tests.{Guid.NewGuid():N}");

    [Fact]
    public async Task Processor_true_should_ack_message()
    {
        FakePubSubSubscriberClient client = new();
        using LedgerEventsPubSubConsumer sut = CreateSut(client, (_, _) => Task.FromResult(true));
        await sut.StartAsync(CancellationToken.None);
        await client.WaitUntilStartedAsync();

        SubscriberClient.Reply reply = await client.ProcessAsync(
            CreateMessage(),
            TestContext.Current.CancellationToken);

        Assert.Equal(SubscriberClient.Reply.Ack, reply);
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Processor_false_should_nack_message()
    {
        FakePubSubSubscriberClient client = new();
        using LedgerEventsPubSubConsumer sut = CreateSut(client, (_, _) => Task.FromResult(false));
        await sut.StartAsync(CancellationToken.None);
        await client.WaitUntilStartedAsync();

        SubscriberClient.Reply reply = await client.ProcessAsync(
            CreateMessage(),
            TestContext.Current.CancellationToken);

        Assert.Equal(SubscriberClient.Reply.Nack, reply);
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Recoverable_exception_should_nack_message()
    {
        FakePubSubSubscriberClient client = new();
        using LedgerEventsPubSubConsumer sut = CreateSut(
            client,
            (_, _) => throw new TimeoutException("Transient failure."));
        await sut.StartAsync(CancellationToken.None);
        await client.WaitUntilStartedAsync();

        SubscriberClient.Reply reply = await client.ProcessAsync(
            CreateMessage(),
            TestContext.Current.CancellationToken);

        Assert.Equal(SubscriberClient.Reply.Nack, reply);
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Unexpected_exception_should_nack_message_and_keep_consumer_active()
    {
        FakePubSubSubscriberClient client = new();
        var attempts = 0;
        using LedgerEventsPubSubConsumer sut = CreateSut(
            client,
            (_, _) =>
            {
                attempts++;
                if (attempts == 1)
                    throw new InvalidOperationException("Unexpected failure.");

                return Task.FromResult(true);
            });
        await sut.StartAsync(CancellationToken.None);
        await client.WaitUntilStartedAsync();

        SubscriberClient.Reply firstReply = await client.ProcessAsync(
            CreateMessage(),
            TestContext.Current.CancellationToken);
        SubscriberClient.Reply secondReply = await client.ProcessAsync(
            CreateMessage(),
            TestContext.Current.CancellationToken);

        Assert.Equal(SubscriberClient.Reply.Nack, firstReply);
        Assert.Equal(SubscriberClient.Reply.Ack, secondReply);
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Handler_cancellation_should_nack_message()
    {
        FakePubSubSubscriberClient client = new();
        using LedgerEventsPubSubConsumer sut = CreateSut(
            client,
            (_, token) => Task.FromCanceled<bool>(token));
        await sut.StartAsync(CancellationToken.None);
        await client.WaitUntilStartedAsync();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        SubscriberClient.Reply reply = await client.ProcessAsync(CreateMessage(), cts.Token);

        Assert.Equal(SubscriberClient.Reply.Nack, reply);
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Cancellation_should_stop_without_unhandled_exception()
    {
        FakePubSubSubscriberClient client = new();
        using LedgerEventsPubSubConsumer sut = CreateSut(client, (_, _) => Task.FromResult(true));
        await sut.StartAsync(CancellationToken.None);
        await client.WaitUntilStartedAsync();

        await sut.StopAsync(CancellationToken.None);

        Assert.Equal(1, client.StopCalls);
    }

    private static LedgerEventsPubSubConsumer CreateSut(
        FakePubSubSubscriberClient client,
        Func<NeutralReceivedMessage, CancellationToken, Task<bool>> processMessageAsync)
    {
        PubSubConsumerOptions options = new()
        {
            ProjectId = "poc-project",
            SubscriptionId = "ledger-events-balance",
            DeadLetterTopicId = "ledger-events-dlq"
        };

        return new LedgerEventsPubSubConsumer(
            Options.Create(options),
            processMessageAsync,
            new FakePubSubSubscriberClientFactory(client),
            NullLogger<LedgerEventsPubSubConsumer>.Instance,
            _metrics);
    }

    private static PubsubMessage CreateMessage()
        => new()
        {
            Data = ByteString.CopyFromUtf8("{}"),
            MessageId = "message-1"
        };

    private sealed class FakePubSubSubscriberClientFactory(FakePubSubSubscriberClient client)
        : IPubSubSubscriberClientFactory
    {
        public Task<IPubSubSubscriberClient> CreateAsync(
            string projectId,
            string subscriptionId,
            int subscriberClientCount,
            CancellationToken cancellationToken)
            => Task.FromResult<IPubSubSubscriberClient>(client);
    }

    private sealed class FakePubSubSubscriberClient : IPubSubSubscriberClient
    {
        private readonly TaskCompletionSource _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _started = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>>? _handler;

        public int StopCalls
        {
            get; private set;
        }

        public Task StartAsync(Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>> handler)
        {
            _handler = handler;
            _started.TrySetResult();
            return _completion.Task;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCalls++;
            _completion.TrySetResult();
            return Task.CompletedTask;
        }

        public Task<SubscriberClient.Reply> ProcessAsync(PubsubMessage message, CancellationToken cancellationToken = default)
        {
            Assert.NotNull(_handler);
            return _handler(message, cancellationToken);
        }

        public Task WaitUntilStartedAsync() => _started.Task;
    }
}
