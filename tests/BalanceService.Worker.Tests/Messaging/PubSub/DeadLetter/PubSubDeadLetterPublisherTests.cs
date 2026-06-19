using System.Globalization;
using System.Text.Json;

using BalanceService.Worker.Messaging.Abstractions;
using BalanceService.Worker.Messaging.PubSub.Configuration;
using BalanceService.Worker.Messaging.PubSub.DeadLetter;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Options;

namespace BalanceService.Worker.Tests.Messaging.PubSub.DeadLetter;

public sealed class PubSubDeadLetterPublisherTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PublishAsync_should_serialize_message_and_publish_relevant_attributes()
    {
        FakePubSubDeadLetterPublisherClient client = new();
        await using PubSubDeadLetterPublisher sut = CreateSut(client);
        DeadLetterMessage message = CreateMessage();

        await sut.PublishAsync(message, CancellationToken.None);

        PubsubMessage published = Assert.Single(client.PublishedMessages);
        Assert.Equal(
            JsonSerializer.Serialize(message, JsonOptions),
            published.Data.ToStringUtf8());
        Assert.Equal("LedgerEntryCreated.v1", published.Attributes[MessageAttributeNames.EventType]);
        Assert.Equal("evt-1", published.Attributes[MessageAttributeNames.EventId]);
        Assert.Equal("corr-1", published.Attributes[MessageAttributeNames.CorrelationId]);
        Assert.Equal("traceparent", published.Attributes[MessageAttributeNames.TraceParent]);
        Assert.Equal("tracestate", published.Attributes[MessageAttributeNames.TraceState]);
        Assert.Equal("tenant=poc", published.Attributes[MessageAttributeNames.Baggage]);
        Assert.Equal("Deserialization failed.", published.Attributes["dlq_reason"]);
        Assert.Equal("ledger-events-balance", published.Attributes["original_source"]);
        Assert.Equal("pubsub", published.Attributes["original_provider"]);
        Assert.False(published.Attributes.ContainsKey("ignored"));
    }

    [Fact]
    public async Task PublishAsync_should_include_original_metadata_with_neutral_names()
    {
        FakePubSubDeadLetterPublisherClient client = new();
        await using PubSubDeadLetterPublisher sut = CreateSut(client);

        await sut.PublishAsync(CreateMessage(), CancellationToken.None);

        PubsubMessage published = Assert.Single(client.PublishedMessages);
        Assert.Equal("message-1", published.Attributes["original_metadata_message_id"]);
        Assert.Equal("ledger-events-balance", published.Attributes["original_metadata_subscription_id"]);
        Assert.Equal("3", published.Attributes["original_metadata_delivery_attempt"]);
        Assert.False(published.Attributes.ContainsKey("original_topic"));
        Assert.False(published.Attributes.ContainsKey("original_partition"));
        Assert.False(published.Attributes.ContainsKey("original_offset"));
    }

    [Fact]
    public async Task PublishAsync_should_validate_dead_letter_topic_before_creating_client()
    {
        FakePubSubDeadLetterPublisherClientFactory factory = new(new FakePubSubDeadLetterPublisherClient());
        await using PubSubDeadLetterPublisher sut = CreateSut(factory, deadLetterTopicId: "");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.PublishAsync(CreateMessage(), CancellationToken.None));

        Assert.Contains("DeadLetterTopicId", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, factory.CreateCalls);
    }

    [Fact]
    public async Task DisposeAsync_should_ignore_shutdown_failure_after_client_creation()
    {
        FakePubSubDeadLetterPublisherClient client = new()
        {
            ShutdownException = new InvalidOperationException("Shutdown failure.")
        };
        await using PubSubDeadLetterPublisher sut = CreateSut(client);

        await sut.PublishAsync(CreateMessage(), CancellationToken.None);

        await sut.DisposeAsync();
        Assert.Equal(1, client.ShutdownCalls);
    }

    private static PubSubDeadLetterPublisher CreateSut(
        FakePubSubDeadLetterPublisherClient client,
        string deadLetterTopicId = "ledger-events-dlq")
        => CreateSut(new FakePubSubDeadLetterPublisherClientFactory(client), deadLetterTopicId);

    private static PubSubDeadLetterPublisher CreateSut(
        FakePubSubDeadLetterPublisherClientFactory factory,
        string deadLetterTopicId = "ledger-events-dlq")
    {
        PubSubConsumerOptions options = new()
        {
            ProjectId = "poc-project",
            SubscriptionId = "ledger-events-balance",
            DeadLetterTopicId = deadLetterTopicId
        };

        return new PubSubDeadLetterPublisher(Options.Create(options), factory);
    }

    private static DeadLetterMessage CreateMessage()
        => new(
            "{invalid-json",
            "ledger-events-balance",
            "pubsub",
            "LedgerEntryCreated.v1",
            "Deserialization failed.",
            nameof(JsonException),
            DateTimeOffset.Parse("2026-06-01T12:34:56.0000000+00:00", CultureInfo.InvariantCulture),
            new Dictionary<string, string>
            {
                [MessageAttributeNames.EventType] = "LedgerEntryCreated.v1",
                [MessageAttributeNames.EventId] = "evt-1",
                [MessageAttributeNames.CorrelationId] = "corr-1",
                [MessageAttributeNames.TraceParent] = "traceparent",
                [MessageAttributeNames.TraceState] = "tracestate",
                [MessageAttributeNames.Baggage] = "tenant=poc",
                ["ignored"] = "value"
            },
            new Dictionary<string, string>
            {
                ["subscription_id"] = "ledger-events-balance",
                ["message_id"] = "message-1",
                ["delivery_attempt"] = "3"
            });

    private sealed class FakePubSubDeadLetterPublisherClientFactory
        (FakePubSubDeadLetterPublisherClient client)
        : IPubSubDeadLetterPublisherClientFactory
    {
        public int CreateCalls
        {
            get; private set;
        }

        public Task<IPubSubDeadLetterPublisherClient> CreateAsync(string projectId, string topicId)
        {
            CreateCalls++;
            return Task.FromResult<IPubSubDeadLetterPublisherClient>(client);
        }
    }

    private sealed class FakePubSubDeadLetterPublisherClient : IPubSubDeadLetterPublisherClient
    {
        public List<PubsubMessage> PublishedMessages { get; } = new();
        public Exception? ShutdownException
        {
            get; set;
        }
        public int ShutdownCalls
        {
            get; private set;
        }

        public Task<string> PublishAsync(PubsubMessage message, CancellationToken cancellationToken)
        {
            PublishedMessages.Add(message);
            return Task.FromResult("dlq-message-id");
        }

        public Task ShutdownAsync(TimeSpan timeout)
        {
            ShutdownCalls++;
            return ShutdownException is null ? Task.CompletedTask : throw ShutdownException;
        }
    }
}
