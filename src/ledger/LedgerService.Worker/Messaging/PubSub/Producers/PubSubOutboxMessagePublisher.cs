using System.Collections.Concurrent;
using System.Diagnostics;

using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using LedgerService.Application.Abstractions.Messaging;
using LedgerService.Worker.Messaging.Abstractions;
using LedgerService.Worker.Messaging.PubSub.Configuration;
using LedgerService.Worker.Messaging.PubSub.Tracing;

using Microsoft.Extensions.Options;

namespace LedgerService.Worker.Messaging.PubSub.Producers;

public sealed class PubSubOutboxMessagePublisher : IOutboxMessagePublisher, IAsyncDisposable
{
    private readonly PubSubProducerOptions _options;
    private readonly IPubSubPublisherClientFactory _clientFactory;
    private readonly ConcurrentDictionary<string, Lazy<Task<IPubSubPublisherClient>>> _clients = new();

    public PubSubOutboxMessagePublisher(IOptions<PubSubProducerOptions> options)
        : this(options, new GooglePubSubPublisherClientFactory())
    {
    }

    internal PubSubOutboxMessagePublisher(
        IOptions<PubSubProducerOptions> options,
        IPubSubPublisherClientFactory clientFactory)
    {
        _options = options.Value;
        _clientFactory = clientFactory;
    }

    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        string topicId = ResolveDestination(message);
        PubsubMessage pubSubMessage = CreatePubSubMessage(message);

        try
        {
            IPubSubPublisherClient client = await GetClientAsync(topicId);
            await client.PublishAsync(pubSubMessage, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new MessagePublishException(
                $"Failed to publish outbox message {message.Id} to destination '{topicId}'.",
                ex);
        }
    }

    public string ResolveDestination(OutboxMessage message)
    {
        return _options.TopicMap.TryGetValue(message.EventType, out string? mapped) &&
            !string.IsNullOrWhiteSpace(mapped)
            ? mapped
            : _options.DefaultTopicId;
    }

    public async ValueTask DisposeAsync()
    {
        TimeSpan timeout = TimeSpan.FromSeconds(_options.ShutdownTimeoutSeconds);
        foreach (Lazy<Task<IPubSubPublisherClient>> lazyClient in _clients.Values)
        {
            if (!lazyClient.IsValueCreated)
                continue;

            try
            {
                IPubSubPublisherClient client = await lazyClient.Value;
                await client.ShutdownAsync(timeout);
            }
            catch (Exception)
            {
                // Shutdown is best effort during worker disposal.
            }
        }
    }

    private PubsubMessage CreatePubSubMessage(OutboxMessage message)
    {
        PubsubMessage pubSubMessage = new()
        {
            Data = ByteString.CopyFromUtf8(message.Payload),
            OrderingKey = _options.EnableMessageOrdering
                ? message.AggregateId.ToString("N")
                : string.Empty
        };

        pubSubMessage.Attributes.Add(PubSubAttributeNames.EventId, message.Id.ToString());
        pubSubMessage.Attributes.Add(PubSubAttributeNames.EventType, message.EventType);
        if (message.CorrelationId is not null)
        {
            pubSubMessage.Attributes.Add(
                PubSubAttributeNames.CorrelationId,
                message.CorrelationId.Value.ToString());
        }

        Activity? activity = Activity.Current;
        PubSubTraceContext.AddPropagationAttributes(
            pubSubMessage.Attributes,
            message.TraceParent ?? activity?.Id,
            message.TraceState ?? activity?.TraceStateString,
            message.Baggage ?? PubSubTraceContext.FormatCurrentBaggage());

        return pubSubMessage;
    }

    private async Task<IPubSubPublisherClient> GetClientAsync(string topicId)
    {
        Lazy<Task<IPubSubPublisherClient>> lazyClient = _clients.GetOrAdd(
            topicId,
            destination => new Lazy<Task<IPubSubPublisherClient>>(
                () => _clientFactory.CreateAsync(
                    _options.ProjectId,
                    destination,
                    _options.EnableMessageOrdering),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazyClient.Value;
        }
        catch
        {
            _clients.TryRemove(new KeyValuePair<string, Lazy<Task<IPubSubPublisherClient>>>(
                topicId,
                lazyClient));
            throw;
        }
    }
}

internal interface IPubSubPublisherClientFactory
{
    Task<IPubSubPublisherClient> CreateAsync(
        string projectId,
        string topicId,
        bool enableMessageOrdering);
}

internal interface IPubSubPublisherClient
{
    Task<string> PublishAsync(PubsubMessage message, CancellationToken cancellationToken);

    Task ShutdownAsync(TimeSpan timeout);
}

internal sealed class GooglePubSubPublisherClientFactory : IPubSubPublisherClientFactory
{
    public async Task<IPubSubPublisherClient> CreateAsync(
        string projectId,
        string topicId,
        bool enableMessageOrdering)
    {
        PublisherClientBuilder builder = new()
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
            TopicName = TopicName.FromProjectTopic(projectId, topicId),
            Settings = new PublisherClient.Settings
            {
                EnableMessageOrdering = enableMessageOrdering
            }
        };

        PublisherClient client = await builder.BuildAsync(CancellationToken.None);
        return new GooglePubSubPublisherClient(client);
    }
}

internal sealed class GooglePubSubPublisherClient(PublisherClient client) : IPubSubPublisherClient
{
    public Task<string> PublishAsync(PubsubMessage message, CancellationToken cancellationToken)
        => client.PublishAsync(message).WaitAsync(cancellationToken);

    public Task ShutdownAsync(TimeSpan timeout)
        => client.ShutdownAsync(timeout);
}
