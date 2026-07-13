using Confluent.Kafka;

using LedgerService.Worker.Messaging.Kafka.Configuration;
using LedgerService.Worker.Messaging.Processors;

using Microsoft.Extensions.Options;

namespace LedgerService.Worker.Messaging.Kafka.Consumers;

public sealed partial class ReprocessamentoLancamentosConsumerService : BackgroundService
{
    private readonly ReprocessamentoLancamentosConsumerOptions _options;
    private readonly ReprocessamentoLancamentosMessageProcessor _messageProcessor;
    private readonly ILogger<ReprocessamentoLancamentosConsumerService> _logger;

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Kafka consumer de reprocessamento reportou erro: {Reason} (IsFatal={IsFatal})")]
    private static partial void LogKafkaConsumerError(ILogger logger, string reason, bool isFatal);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Consumer de reprocessamento iniciado. groupId={GroupId} clientId={ClientId} topic={Topic}")]
    private static partial void LogConsumerStarted(ILogger logger, string groupId, string clientId, string topic);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Falha ao consumir mensagem de reprocessamento.")]
    private static partial void LogConsumeFailure(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Falha ao processar mensagem de reprocessamento. provider={TransportProvider} source={TransportSource} partition={TransportPartition} offset={TransportOffset}")]
    private static partial void LogKafkaProcessingFailure(ILogger logger, Exception exception, string transportProvider, string? transportSource, int? transportPartition, long? transportOffset);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Timeout ao processar mensagem de reprocessamento. provider={TransportProvider} source={TransportSource} partition={TransportPartition} offset={TransportOffset}")]
    private static partial void LogKafkaProcessingTimeout(ILogger logger, Exception exception, string transportProvider, string? transportSource, int? transportPartition, long? transportOffset);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Falha inesperada ao processar mensagem de reprocessamento. provider={TransportProvider} source={TransportSource} partition={TransportPartition} offset={TransportOffset}")]
    private static partial void LogUnexpectedKafkaProcessingFailure(ILogger logger, Exception exception, string transportProvider, string? transportSource, int? transportPartition, long? transportOffset);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Consumer de reprocessamento parado.")]
    private static partial void LogConsumerStopped(ILogger logger);

    public ReprocessamentoLancamentosConsumerService(
        IOptions<ReprocessamentoLancamentosConsumerOptions> options,
        ReprocessamentoLancamentosMessageProcessor messageProcessor,
        ILogger<ReprocessamentoLancamentosConsumerService> logger)
    {
        _options = options.Value;
        _messageProcessor = messageProcessor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateOptions(_options);

        using var consumer = new ConsumerBuilder<string, string>(CreateConsumerConfig(_options))
            .SetErrorHandler((_, e) =>
            {
                LogKafkaConsumerError(_logger, e.Reason, e.IsFatal);
            })
            .Build();

        consumer.Subscribe(_options.Topic);
        LogConsumerStarted(_logger, _options.GroupId, _options.ClientId, _options.Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = consumer.Consume(stoppingToken);
                await ProcessConsumeResultAsync(consumer, result, stoppingToken);
            }
            catch (ConsumeException ex)
            {
                LogConsumeFailure(_logger, ex);
                await Task.Delay(_options.ConsumeErrorRetryDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (KafkaException ex)
            {
                LogKafkaProcessingFailure(
                    _logger,
                    ex,
                    "kafka",
                    result?.Topic,
                    result?.Partition.Value,
                    result?.Offset.Value);
                await Task.Delay(_options.ProcessingErrorRetryDelay, stoppingToken);
            }
            catch (TimeoutException ex)
            {
                LogKafkaProcessingTimeout(
                    _logger,
                    ex,
                    "kafka",
                    result?.Topic,
                    result?.Partition.Value,
                    result?.Offset.Value);
                await Task.Delay(_options.ProcessingErrorRetryDelay, stoppingToken);
            }
            catch (Exception ex)
            {
                LogUnexpectedKafkaProcessingFailure(
                    _logger,
                    ex,
                    "kafka",
                    result?.Topic,
                    result?.Partition.Value,
                    result?.Offset.Value);
                await Task.Delay(_options.ProcessingErrorRetryDelay, stoppingToken);
            }
        }

        CloseConsumer(consumer);

        LogConsumerStopped(_logger);
    }

    private static ConsumerConfig CreateConsumerConfig(ReprocessamentoLancamentosConsumerOptions options)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = options.GroupId,
            ClientId = options.ClientId,
            EnableAutoCommit = options.EnableAutoCommit,
            EnableAutoOffsetStore = options.EnableAutoOffsetStore,
            AllowAutoCreateTopics = options.AllowAutoCreateTopics,
            AutoOffsetReset = ParseAutoOffsetReset(options.AutoOffsetReset)
        };
        config.ApplySecurity(options);

        return config;
    }

    private async Task ProcessConsumeResultAsync(
        IConsumer<string, string> consumer,
        ConsumeResult<string, string>? result,
        CancellationToken stoppingToken)
    {
        if (result?.Message?.Value is null)
        {
            return;
        }

        var message = KafkaReprocessamentoReceivedMessageMapper.Map(result);
        if (await _messageProcessor.ProcessAsync(message, stoppingToken))
        {
            consumer.Commit(result);
        }
    }

    private static void CloseConsumer(IConsumer<string, string> consumer)
    {
        try
        {
            consumer.Close();
        }
        catch (KafkaException)
        {
            // ignore shutdown errors
        }
    }

    internal static void ValidateOptions(ReprocessamentoLancamentosConsumerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BootstrapServers))
            throw new InvalidOperationException("Reprocessamentos Consumer BootstrapServers nao configurado.");

        if (string.IsNullOrWhiteSpace(options.GroupId))
            throw new InvalidOperationException("Reprocessamentos Consumer GroupId nao configurado.");

        if (string.IsNullOrWhiteSpace(options.Topic))
            throw new InvalidOperationException("Reprocessamentos Consumer Topic nao configurado.");

        if (options.ConsumeErrorRetryDelay <= TimeSpan.Zero)
            throw new InvalidOperationException("Reprocessamentos Consumer ConsumeErrorRetryDelay deve ser maior que zero.");

        if (options.ProcessingErrorRetryDelay <= TimeSpan.Zero)
            throw new InvalidOperationException("Reprocessamentos Consumer ProcessingErrorRetryDelay deve ser maior que zero.");
    }

    internal static AutoOffsetReset ParseAutoOffsetReset(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "earliest" => AutoOffsetReset.Earliest,
            "latest" => AutoOffsetReset.Latest,
            _ => AutoOffsetReset.Earliest
        };
    }
}
