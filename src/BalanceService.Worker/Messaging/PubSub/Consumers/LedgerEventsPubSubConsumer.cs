using BalanceService.Worker.Messaging.Processors;
using BalanceService.Worker.Messaging.PubSub.Configuration;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NeutralReceivedMessage = BalanceService.Worker.Messaging.Abstractions.ReceivedMessage;

namespace BalanceService.Worker.Messaging.PubSub.Consumers;

public sealed class LedgerEventsPubSubConsumer : BackgroundService
{
    private readonly PubSubConsumerOptions _options;
    private readonly Func<NeutralReceivedMessage, CancellationToken, Task<bool>> _processMessageAsync;
    private readonly IPubSubSubscriberClientFactory _clientFactory;
    private readonly ILogger<LedgerEventsPubSubConsumer> _logger;

    public LedgerEventsPubSubConsumer(
        IOptions<PubSubConsumerOptions> options,
        LedgerEntryCreatedMessageProcessor messageProcessor,
        ILogger<LedgerEventsPubSubConsumer> logger)
        : this(
            options,
            messageProcessor.ProcessAsync,
            new GooglePubSubSubscriberClientFactory(),
            logger)
    {
    }

    internal LedgerEventsPubSubConsumer(
        IOptions<PubSubConsumerOptions> options,
        Func<NeutralReceivedMessage, CancellationToken, Task<bool>> processMessageAsync,
        IPubSubSubscriberClientFactory clientFactory,
        ILogger<LedgerEventsPubSubConsumer> logger)
    {
        _options = options.Value;
        _processMessageAsync = processMessageAsync;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateOptions(_options);

        IPubSubSubscriberClient client = await _clientFactory.CreateAsync(
            _options.ProjectId,
            _options.SubscriptionId,
            _options.SubscriberClientCount,
            stoppingToken);

        Task subscriberTask = client.StartAsync(ProcessMessageAsync);
        Task cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);

        try
        {
            await Task.WhenAny(subscriberTask, cancellationTask);

            if (subscriberTask.IsCompleted)
                await subscriberTask;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during hosted service shutdown.
        }
        finally
        {
            try
            {
                await client.StopAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao encerrar consumer Pub/Sub.");
            }
        }
    }

    internal static void ValidateOptions(PubSubConsumerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ProjectId))
            throw new InvalidOperationException("PubSub ProjectId nao configurado.");

        if (string.IsNullOrWhiteSpace(options.SubscriptionId))
            throw new InvalidOperationException("PubSub SubscriptionId nao configurado.");
    }

    private async Task<SubscriberClient.Reply> ProcessMessageAsync(
        PubsubMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            NeutralReceivedMessage receivedMessage = PubSubReceivedMessageMapper.Map(
                message,
                _options.SubscriptionId);

            return await _processMessageAsync(receivedMessage, cancellationToken)
                ? SubscriberClient.Reply.Ack
                : SubscriberClient.Reply.Nack;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return SubscriberClient.Reply.Nack;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Falha recuperavel ao processar mensagem Pub/Sub. subscription={SubscriptionId} messageId={MessageId}",
                _options.SubscriptionId,
                message.MessageId);

            return SubscriberClient.Reply.Nack;
        }
    }
}

internal interface IPubSubSubscriberClientFactory
{
    Task<IPubSubSubscriberClient> CreateAsync(
        string projectId,
        string subscriptionId,
        int subscriberClientCount,
        CancellationToken cancellationToken);
}

internal interface IPubSubSubscriberClient
{
    Task StartAsync(Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>> handler);

    Task StopAsync(CancellationToken cancellationToken);
}

internal sealed class GooglePubSubSubscriberClientFactory : IPubSubSubscriberClientFactory
{
    public async Task<IPubSubSubscriberClient> CreateAsync(
        string projectId,
        string subscriptionId,
        int subscriberClientCount,
        CancellationToken cancellationToken)
    {
        SubscriberClientBuilder builder = new()
        {
            SubscriptionName = SubscriptionName.FromProjectSubscription(projectId, subscriptionId),
            ClientCount = subscriberClientCount
        };

        SubscriberClient client = await builder.BuildAsync(cancellationToken);
        return new GooglePubSubSubscriberClient(client);
    }
}

internal sealed class GooglePubSubSubscriberClient(SubscriberClient client) : IPubSubSubscriberClient
{
    public Task StartAsync(Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>> handler)
        => client.StartAsync(handler);

    public Task StopAsync(CancellationToken cancellationToken)
        => client.StopAsync(cancellationToken);
}
