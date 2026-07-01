using Google.Cloud.PubSub.V1;

using LedgerService.Domain.Entities;
using LedgerService.Worker.Messaging.Abstractions;
using LedgerService.Worker.Messaging.PubSub.Configuration;
using LedgerService.Worker.Messaging.PubSub.Producers;
using LedgerService.Worker.Messaging.PubSub.Tracing;

using Microsoft.Extensions.Options;

namespace LedgerService.Worker.Tests.Messaging.PubSub.Producers;

public sealed class PubSubOutboxMessagePublisherTests
{
    [Fact]
    public async Task ResolveDestination_should_use_topic_map_for_event_type()
    {
        await using PubSubOutboxMessagePublisher sut = CreateSut(
            new FakePubSubPublisherClient(),
            topicMap: new Dictionary<string, string>
            {
                ["LedgerEntryCreated.v1"] = "ledger-entry-created"
            });

        string destination = sut.ResolveDestination(CreateOutboxMessage());

        Assert.Equal("ledger-entry-created", destination);
    }

    [Fact]
    public async Task ResolveDestination_should_use_default_topic_when_event_type_is_not_mapped()
    {
        await using PubSubOutboxMessagePublisher sut = CreateSut(new FakePubSubPublisherClient());

        string destination = sut.ResolveDestination(CreateOutboxMessage());

        Assert.Equal("ledger-events", destination);
    }

    [Fact]
    public async Task PublishAsync_should_publish_payload_and_attributes()
    {
        FakePubSubPublisherClient client = new();
        await using PubSubOutboxMessagePublisher sut = CreateSut(client);
        Guid correlationId = Guid.NewGuid();
        OutboxMessage message = CreateOutboxMessage(
            correlationId: correlationId,
            traceParent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            traceState: "vendor=value",
            baggage: "merchant_id=merchant-1");

        await sut.PublishAsync(message, CancellationToken.None);

        PubsubMessage published = Assert.Single(client.PublishedMessages);
        Assert.Equal(message.Payload, published.Data.ToStringUtf8());
        Assert.Equal(message.Id.ToString(), published.Attributes[PubSubAttributeNames.EventId]);
        Assert.Equal(message.EventType, published.Attributes[PubSubAttributeNames.EventType]);
        Assert.Equal(correlationId.ToString(), published.Attributes[PubSubAttributeNames.CorrelationId]);
        Assert.Equal(message.TraceParent, published.Attributes[PubSubAttributeNames.TraceParent]);
        Assert.Equal(message.TraceState, published.Attributes[PubSubAttributeNames.TraceState]);
        Assert.Equal(message.Baggage, published.Attributes[PubSubAttributeNames.Baggage]);
    }

    [Fact]
    public async Task PublishAsync_should_use_aggregate_id_as_ordering_key_when_enabled()
    {
        FakePubSubPublisherClient client = new();
        await using PubSubOutboxMessagePublisher sut = CreateSut(client, enableMessageOrdering: true);
        OutboxMessage message = CreateOutboxMessage();

        await sut.PublishAsync(message, CancellationToken.None);

        PubsubMessage published = Assert.Single(client.PublishedMessages);
        Assert.Equal(message.AggregateId.ToString("N"), published.OrderingKey);
    }

    [Fact]
    public async Task PublishAsync_should_convert_publish_failure_to_message_publish_exception()
    {
        InvalidOperationException failure = new("Pub/Sub unavailable.");
        await using PubSubOutboxMessagePublisher sut = CreateSut(new FakePubSubPublisherClient(failure));
        OutboxMessage message = CreateOutboxMessage();

        MessagePublishException exception = await Assert.ThrowsAsync<MessagePublishException>(
            () => sut.PublishAsync(message, CancellationToken.None));

        Assert.Same(failure, exception.InnerException);
        Assert.Contains(message.Id.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("ledger-events", exception.Message, StringComparison.Ordinal);
    }

    private static PubSubOutboxMessagePublisher CreateSut(
        FakePubSubPublisherClient client,
        bool enableMessageOrdering = false,
        Dictionary<string, string>? topicMap = null)
    {
        PubSubProducerOptions options = new()
        {
            ProjectId = "poc-project",
            DefaultTopicId = "ledger-events",
            EnableMessageOrdering = enableMessageOrdering,
            TopicMap = topicMap ?? new Dictionary<string, string>()
        };

        return new PubSubOutboxMessagePublisher(
            Options.Create(options),
            new FakePubSubPublisherClientFactory(client));
    }

    private static OutboxMessage CreateOutboxMessage(
        Guid? correlationId = null,
        string? traceParent = null,
        string? traceState = null,
        string? baggage = null)
        => new(
            "LedgerEntry",
            Guid.NewGuid(),
            "LedgerEntryCreated.v1",
            """{"amount":10}""",
            DateTime.UtcNow,
            correlationId,
            traceParent,
            traceState,
            baggage);

    private sealed class FakePubSubPublisherClientFactory(FakePubSubPublisherClient client)
        : IPubSubPublisherClientFactory
    {
        public Task<IPubSubPublisherClient> CreateAsync(
            string projectId,
            string topicId,
            bool enableMessageOrdering)
            => Task.FromResult<IPubSubPublisherClient>(client);
    }

    private sealed class FakePubSubPublisherClient(Exception? failure = null) : IPubSubPublisherClient
    {
        public List<PubsubMessage> PublishedMessages { get; } = new();

        public Task<string> PublishAsync(PubsubMessage message, CancellationToken cancellationToken)
        {
            if (failure is not null)
                return Task.FromException<string>(failure);

            PublishedMessages.Add(message);
            return Task.FromResult("message-id");
        }

        public Task ShutdownAsync(TimeSpan timeout) => Task.CompletedTask;
    }
}
