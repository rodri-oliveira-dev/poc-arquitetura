using BalanceService.Worker.Messaging.Processors;
using BalanceService.Worker.Messaging.PubSub.Configuration;
using BalanceService.Worker.Observability;

using Google.Api.Gax;
using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Options;

using NeutralReceivedMessage = BalanceService.Worker.Messaging.Abstractions.ReceivedMessage;

namespace BalanceService.Worker.Messaging.PubSub.Consumers;

public sealed class LedgerEventsPubSubConsumer : BackgroundService
{
    private readonly PubSubConsumerOptions _options;
    private readonly Func<NeutralReceivedMessage, CancellationToken, Task<bool>> _processMessageAsync;
    private readonly IPubSubSubscriberClientFactory _clientFactory;
    private readonly ILogger<LedgerEventsPubSubConsumer> _logger;
    private readonly MessagingMetrics _metrics;

    public LedgerEventsPubSubConsumer(
        IOptions<PubSubConsumerOptions> options,
        LedgerEntryCreatedMessageProcessor messageProcessor,
        ILogger<LedgerEventsPubSubConsumer> logger,
        MessagingMetrics metrics)
        : this(
            options,
            messageProcessor.ProcessAsync,
            new GooglePubSubSubscriberClientFactory(),
            logger,
            metrics)
    {
    }

    internal LedgerEventsPubSubConsumer(
        IOptions<PubSubConsumerOptions> options,
        Func<NeutralReceivedMessage, CancellationToken, Task<bool>> processMessageAsync,
        IPubSubSubscriberClientFactory clientFactory,
        ILogger<LedgerEventsPubSubConsumer> logger,
        MessagingMetrics metrics)
    {
        _options = options.Value;
        _processMessageAsync = processMessageAsync;
        _clientFactory = clientFactory;
        _logger = logger;
        _metrics = metrics;
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
#pragma warning disable CA1031
            // Encerramento do subscriber e best effort; falha fica em log estruturado e nao deve bloquear shutdown do host.
            catch (Exception ex)
#pragma warning restore CA1031
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
#pragma warning disable CA1031
        // Captura desconhecida intencional por mensagem: Nack preserva retry/DLQ do Pub/Sub e mantem o consumer ativo.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _metrics.RecordConsumerError(_options.SubscriptionId, "unknown", "unexpected");
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
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
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
