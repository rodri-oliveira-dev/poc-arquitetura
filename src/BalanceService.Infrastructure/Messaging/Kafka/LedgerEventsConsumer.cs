using Confluent.Kafka;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BalanceService.Infrastructure.Messaging.Kafka;

public sealed partial class LedgerEventsConsumer : BackgroundService
{
    private readonly KafkaConsumerOptions _options;
    private readonly LedgerKafkaMessageProcessor _messageProcessor;
    private readonly ILogger<LedgerEventsConsumer> _logger;

    public LedgerEventsConsumer(
        IOptions<KafkaConsumerOptions> options,
        LedgerKafkaMessageProcessor messageProcessor,
        ILogger<LedgerEventsConsumer> logger)
    {
        _options = options.Value;
        _messageProcessor = messageProcessor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateOptions(_options);

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            ClientId = _options.ClientId,
            EnableAutoCommit = _options.EnableAutoCommit,
            EnableAutoOffsetStore = _options.EnableAutoOffsetStore,
            AllowAutoCreateTopics = _options.AllowAutoCreateTopics,
            AutoOffsetReset = ParseAutoOffsetReset(_options.AutoOffsetReset)
        };
        config.ApplySecurity(_options);

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) =>
            {
                LogKafkaConsumerError(_logger, e.Reason, e.IsFatal);
            })
            .Build();

        consumer.Subscribe(_options.Topics);

        LogLedgerEventsConsumerStarted(
            _logger,
            _options.GroupId,
            _options.ClientId,
            string.Join(",", _options.Topics));

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null)
                    continue;

                if (await _messageProcessor.ProcessAsync(result, stoppingToken))
                {
                    consumer.Commit(result);
                    LogKafkaMessageCommitted(_logger);
                }
            }
            catch (ConsumeException ex)
            {
                LogKafkaConsumeError(_logger, ex);
                await Task.Delay(_options.ConsumeErrorRetryDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (DbUpdateException ex)
            {
                LogKafkaProcessingError(_logger, ex, result?.Topic, result?.Partition.Value, result?.Offset.Value);

                await Task.Delay(_options.ProcessingErrorRetryDelay, stoppingToken);
            }
            catch (TimeoutException ex)
            {
                LogKafkaProcessingError(_logger, ex, result?.Topic, result?.Partition.Value, result?.Offset.Value);

                await Task.Delay(_options.ProcessingErrorRetryDelay, stoppingToken);
            }
            catch (KafkaException ex)
            {
                LogKafkaProcessingErrorWithKafkaException(_logger, ex, result?.Topic, result?.Partition.Value, result?.Offset.Value);

                await Task.Delay(_options.ProcessingErrorRetryDelay, stoppingToken);
            }
        }

        try
        {
            consumer.Close();
        }
        catch (KafkaException)
        {
            // ignore shutdown errors
        }

        LogLedgerEventsConsumerStopped(_logger);
    }

    private static void ValidateOptions(KafkaConsumerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BootstrapServers))
            throw new InvalidOperationException("Kafka BootstrapServers não configurado.");

        if (string.IsNullOrWhiteSpace(options.GroupId))
            throw new InvalidOperationException("Kafka GroupId não configurado.");

        if (options.Topics is null || options.Topics.Count == 0)
            throw new InvalidOperationException("Kafka Topics não configurado.");

        if (string.IsNullOrWhiteSpace(options.DeadLetterTopic))
            throw new InvalidOperationException("Kafka DeadLetterTopic não configurado.");

        if (options.InvalidMessageRetryDelay <= TimeSpan.Zero)
            throw new InvalidOperationException("Kafka InvalidMessageRetryDelay deve ser maior que zero.");

        if (options.ConsumeErrorRetryDelay <= TimeSpan.Zero)
            throw new InvalidOperationException("Kafka ConsumeErrorRetryDelay deve ser maior que zero.");

        if (options.ProcessingErrorRetryDelay <= TimeSpan.Zero)
            throw new InvalidOperationException("Kafka ProcessingErrorRetryDelay deve ser maior que zero.");
    }

    private static AutoOffsetReset ParseAutoOffsetReset(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "earliest" => AutoOffsetReset.Earliest,
            "latest" => AutoOffsetReset.Latest,
            _ => AutoOffsetReset.Earliest
        };
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Warning, Message = "Kafka consumer error: {Reason} (IsFatal={IsFatal})")]
    private static partial void LogKafkaConsumerError(ILogger logger, string reason, bool isFatal);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "LedgerEventsConsumer started (groupId={GroupId}, clientId={ClientId}, topics={Topics})")]
    private static partial void LogLedgerEventsConsumerStarted(ILogger logger, string groupId, string clientId, string topics);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Debug, Message = "Mensagem processada/DLQ confirmada e offset commitado")]
    private static partial void LogKafkaMessageCommitted(ILogger logger);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Error, Message = "Erro ao consumir do Kafka. Vai retentar.")]
    private static partial void LogKafkaConsumeError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Error, Message = "Erro ao processar mensagem do Kafka. Offset nao sera commitado (retry). topic={Topic} partition={Partition} offset={Offset}")]
    private static partial void LogKafkaProcessingError(ILogger logger, Exception exception, string? topic, int? partition, long? offset);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Error, Message = "Erro ao processar mensagem do Kafka. Offset não será commitado (retry). topic={Topic} partition={Partition} offset={Offset}")]
    private static partial void LogKafkaProcessingErrorWithKafkaException(ILogger logger, Exception exception, string? topic, int? partition, long? offset);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Information, Message = "LedgerEventsConsumer stopped")]
    private static partial void LogLedgerEventsConsumerStopped(ILogger logger);
}
