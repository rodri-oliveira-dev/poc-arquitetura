using System.Text.Json;

using BalanceService.Worker.Messaging.Abstractions;
using BalanceService.Worker.Messaging.PubSub.Configuration;

using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using Microsoft.Extensions.Options;

namespace BalanceService.Worker.Messaging.PubSub.DeadLetter;

public sealed class PubSubDeadLetterPublisher : IDeadLetterPublisher, IAsyncDisposable
{
    private const string DeadLetterReasonAttribute = "dlq_reason";
    private const string OriginalSourceAttribute = "original_source";
    private const string OriginalProviderAttribute = "original_provider";
    private const string OriginalMetadataPrefix = "original_metadata_";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PubSubConsumerOptions _options;
    private readonly IPubSubDeadLetterPublisherClientFactory _clientFactory;
    private readonly object _clientLock = new();
    private Task<IPubSubDeadLetterPublisherClient>? _clientTask;

    public PubSubDeadLetterPublisher(IOptions<PubSubConsumerOptions> options)
        : this(options, new GooglePubSubDeadLetterPublisherClientFactory())
    {
    }

    internal PubSubDeadLetterPublisher(
        IOptions<PubSubConsumerOptions> options,
        IPubSubDeadLetterPublisherClientFactory clientFactory)
    {
        _options = options.Value;
        _clientFactory = clientFactory;
    }

    public async Task PublishAsync(DeadLetterMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.DeadLetterTopicId))
            throw new InvalidOperationException("PubSub DeadLetterTopicId nao configurado.");

        PubsubMessage pubSubMessage = CreatePubSubMessage(message);
        IPubSubDeadLetterPublisherClient client = await GetClientAsync();
        await client.PublishAsync(pubSubMessage, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        Task<IPubSubDeadLetterPublisherClient>? clientTask;
        lock (_clientLock)
            clientTask = _clientTask;

        if (clientTask is null)
            return;

        try
        {
            IPubSubDeadLetterPublisherClient client = await clientTask;
            await client.ShutdownAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            // Shutdown is best effort during worker disposal.
        }
    }

    private static PubsubMessage CreatePubSubMessage(DeadLetterMessage message)
    {
        PubsubMessage pubSubMessage = new()
        {
            Data = ByteString.CopyFromUtf8(JsonSerializer.Serialize(message, JsonOptions))
        };

        AddAttribute(pubSubMessage, DeadLetterReasonAttribute, message.Reason);
        AddAttribute(pubSubMessage, OriginalSourceAttribute, message.Source);
        AddAttribute(pubSubMessage, OriginalProviderAttribute, message.Provider);
        CopyAttribute(message.Attributes, pubSubMessage, MessageAttributeNames.EventType);
        CopyAttribute(message.Attributes, pubSubMessage, MessageAttributeNames.EventId);
        CopyAttribute(message.Attributes, pubSubMessage, MessageAttributeNames.CorrelationId);
        CopyAttribute(message.Attributes, pubSubMessage, MessageAttributeNames.TraceParent);
        CopyAttribute(message.Attributes, pubSubMessage, MessageAttributeNames.TraceState);
        CopyAttribute(message.Attributes, pubSubMessage, MessageAttributeNames.Baggage);

        foreach ((string name, string value) in message.TransportMetadata)
            AddAttribute(pubSubMessage, $"{OriginalMetadataPrefix}{name}", value);

        return pubSubMessage;
    }

    private async Task<IPubSubDeadLetterPublisherClient> GetClientAsync()
    {
        Task<IPubSubDeadLetterPublisherClient> clientTask;
        lock (_clientLock)
        {
            _clientTask ??= _clientFactory.CreateAsync(
                _options.ProjectId,
                _options.DeadLetterTopicId);
            clientTask = _clientTask;
        }

        try
        {
            return await clientTask;
        }
        catch
        {
            lock (_clientLock)
            {
                if (ReferenceEquals(_clientTask, clientTask))
                    _clientTask = null;
            }

            throw;
        }
    }

    private static void CopyAttribute(
        IReadOnlyDictionary<string, string> source,
        PubsubMessage target,
        string name)
    {
        if (source.TryGetValue(name, out string? value))
            AddAttribute(target, name, value);
    }

    private static void AddAttribute(PubsubMessage message, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            message.Attributes[name] = value;
    }
}

internal interface IPubSubDeadLetterPublisherClientFactory
{
    Task<IPubSubDeadLetterPublisherClient> CreateAsync(string projectId, string topicId);
}

internal interface IPubSubDeadLetterPublisherClient
{
    Task<string> PublishAsync(PubsubMessage message, CancellationToken cancellationToken);

    Task ShutdownAsync(TimeSpan timeout);
}

internal sealed class GooglePubSubDeadLetterPublisherClientFactory : IPubSubDeadLetterPublisherClientFactory
{
    public async Task<IPubSubDeadLetterPublisherClient> CreateAsync(string projectId, string topicId)
    {
        PublisherClientBuilder builder = new()
        {
            TopicName = TopicName.FromProjectTopic(projectId, topicId)
        };

        PublisherClient client = await builder.BuildAsync(CancellationToken.None);
        return new GooglePubSubDeadLetterPublisherClient(client);
    }
}

internal sealed class GooglePubSubDeadLetterPublisherClient(PublisherClient client)
    : IPubSubDeadLetterPublisherClient
{
    public Task<string> PublishAsync(PubsubMessage message, CancellationToken cancellationToken)
        => client.PublishAsync(message).WaitAsync(cancellationToken);

    public Task ShutdownAsync(TimeSpan timeout)
        => client.ShutdownAsync(timeout);
}
