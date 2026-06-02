extern alias LedgerWorker;

using Google.Api.Gax;
using Google.Cloud.PubSub.V1;

using LedgerService.Domain.Entities;

using Microsoft.Extensions.Options;

using PubSubAttributeNames = LedgerWorker::LedgerService.Worker.Messaging.PubSub.Tracing.PubSubAttributeNames;
using PubSubOutboxMessagePublisher = LedgerWorker::LedgerService.Worker.Messaging.PubSub.Producers.PubSubOutboxMessagePublisher;
using PubSubProducerOptions = LedgerWorker::LedgerService.Worker.Messaging.PubSub.Configuration.PubSubProducerOptions;

namespace LedgerService.IntegrationTests.Messaging.PubSub;

public sealed class PubSubOutboxMessagePublisherEmulatorTests
{
    private const string EmulatorHostVariable = "PUBSUB_EMULATOR_HOST";
    private const string ProjectIdVariable = "PUBSUB_PROJECT_ID";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishAsync_should_deliver_message_with_attributes_and_optional_ordering_key(
        bool enableMessageOrdering)
    {
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EmulatorHostVariable)),
            $"{EmulatorHostVariable} nao definido; teste opcional requer Pub/Sub emulator.");

        string projectId = Environment.GetEnvironmentVariable(ProjectIdVariable)
            ?? "poc-integration-tests";
        string suffix = Guid.NewGuid().ToString("N");
        TopicName topicName = TopicName.FromProjectTopic(projectId, $"ledger-events-{suffix}");
        SubscriptionName subscriptionName = SubscriptionName.FromProjectSubscription(
            projectId,
            $"ledger-events-{suffix}");

        PublisherServiceApiClient publisherAdmin = await new PublisherServiceApiClientBuilder
        {
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.BuildAsync(TestContext.Current.CancellationToken);
        SubscriberServiceApiClient subscriberAdmin = await new SubscriberServiceApiClientBuilder
        {
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.BuildAsync(TestContext.Current.CancellationToken);

        await publisherAdmin.CreateTopicAsync(
            topicName,
            TestContext.Current.CancellationToken);
        await subscriberAdmin.CreateSubscriptionAsync(
            new Subscription
            {
                SubscriptionName = subscriptionName,
                TopicAsTopicName = topicName,
                EnableMessageOrdering = enableMessageOrdering
            },
            TestContext.Current.CancellationToken);

        try
        {
            var options = Options.Create(new PubSubProducerOptions
            {
                ProjectId = projectId,
                DefaultTopicId = topicName.TopicId,
                EnableMessageOrdering = enableMessageOrdering,
                ShutdownTimeoutSeconds = 5
            });
            await using var sut = new PubSubOutboxMessagePublisher(options);
            Guid correlationId = Guid.NewGuid();
            var message = new OutboxMessage(
                aggregateType: "LedgerEntry",
                aggregateId: Guid.NewGuid(),
                eventType: "LedgerEntryCreated.v1",
                payload: """{"amount":10}""",
                occurredAt: DateTime.UtcNow,
                correlationId,
                traceParent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
                traceState: "vendor=value",
                baggage: "merchant_id=merchant-1");

            await sut.PublishAsync(message, TestContext.Current.CancellationToken);

            PullResponse response = await subscriberAdmin.PullAsync(
                subscriptionName,
                returnImmediately: false,
                maxMessages: 1,
                TestContext.Current.CancellationToken);

            ReceivedMessage received = Assert.Single(response.ReceivedMessages);
            Assert.Equal(message.Payload, received.Message.Data.ToStringUtf8());
            Assert.Equal(message.Id.ToString(), received.Message.Attributes[PubSubAttributeNames.EventId]);
            Assert.Equal(message.EventType, received.Message.Attributes[PubSubAttributeNames.EventType]);
            Assert.Equal(correlationId.ToString(), received.Message.Attributes[PubSubAttributeNames.CorrelationId]);
            Assert.Equal(message.TraceParent, received.Message.Attributes[PubSubAttributeNames.TraceParent]);
            Assert.Equal(message.TraceState, received.Message.Attributes[PubSubAttributeNames.TraceState]);
            Assert.Equal(message.Baggage, received.Message.Attributes[PubSubAttributeNames.Baggage]);
            Assert.Equal(
                enableMessageOrdering ? message.AggregateId.ToString("N") : string.Empty,
                received.Message.OrderingKey);
        }
        finally
        {
            await subscriberAdmin.DeleteSubscriptionAsync(
                subscriptionName,
                TestContext.Current.CancellationToken);
            await publisherAdmin.DeleteTopicAsync(
                topicName,
                TestContext.Current.CancellationToken);
        }
    }
}
