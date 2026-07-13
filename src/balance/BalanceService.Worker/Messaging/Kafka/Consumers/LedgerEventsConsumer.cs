using BalanceService.Worker.Messaging.Kafka.Configuration;
using BalanceService.Worker.Messaging.Kafka.Tracing;
using BalanceService.Worker.Messaging.Processors;
using BalanceService.Worker.Observability;

using Confluent.Kafka;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BalanceService.Worker.Messaging.Kafka.Consumers;

public sealed class LedgerEventsConsumer : BackgroundService
{
    private readonly KafkaConsumerOptions _options;
    private readonly LedgerEntryCreatedMessageProcessor _messageProcessor;
    private readonly MessagingMetrics _metrics;
    private readonly ILogger<LedgerEventsConsumer> _logger;

    public LedgerEventsConsumer(
        IOptions<KafkaConsumerOptions> options,
        LedgerEntryCreatedMessageProcessor messageProcessor,
        MessagingMetrics metrics,
        ILogger<LedgerEventsConsumer> logger)
    {
        _options = options.Value;
        _messageProcessor = messageProcessor;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateOptions(_options);

        using var consumer = new ConsumerBuilder<string, string>(CreateConsumerConfig(_options))
            .SetErrorHandler((_, e) =>
            {
                _logger.KafkaConsumerError(e.Reason, e.IsFatal);
            })
            .Build();

        consumer.Subscribe(_options.Topics);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.ConsumerStarted(
                _options.GroupId,
                _options.ClientId,
                string.Join(",", _options.Topics));
        }

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
                RecordConsumerError(result, ex);
                _logger.KafkaConsumeError(ex);
                await Task.Delay(_options.ConsumeErrorRetryDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (DbUpdateException ex)
            {
                RecordConsumerError(result, ex);
                _logger.KafkaProcessingError(ex, result?.Topic, result?.Partition.Value, result?.Offset.Value);

                await Task.Delay(_options.ProcessingErrorRetryDelay, stoppingToken);
            }
            catch (TimeoutException ex)
            {
                RecordConsumerError(result, ex);
                _logger.KafkaProcessingError(ex, result?.Topic, result?.Partition.Value, result?.Offset.Value);

                await Task.Delay(_options.ProcessingErrorRetryDelay, stoppingToken);
            }
            catch (KafkaException ex)
            {
                RecordConsumerError(result, ex);
                _logger.KafkaProcessingErrorWithKafkaException(ex, result?.Topic, result?.Partition.Value, result?.Offset.Value);

                await Task.Delay(_options.ProcessingErrorRetryDelay, stoppingToken);
            }
        }

        CloseConsumer(consumer);

        _logger.ConsumerStopped();
    }

    private static ConsumerConfig CreateConsumerConfig(KafkaConsumerOptions options)
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

        var message = KafkaReceivedMessageMapper.Map(result);
        if (await _messageProcessor.ProcessAsync(message, stoppingToken))
        {
            consumer.Commit(result);
            _logger.KafkaMessageCommitted();
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

    internal static void ValidateOptions(KafkaConsumerOptions options)
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

    internal static AutoOffsetReset ParseAutoOffsetReset(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "earliest" => AutoOffsetReset.Earliest,
            "latest" => AutoOffsetReset.Latest,
            _ => AutoOffsetReset.Earliest
        };
    }

    private void RecordConsumerError(ConsumeResult<string, string>? result, Exception exception)
    {
        var topic = result?.Topic ?? "unknown";
        var eventType = "unknown";

        if (result?.Message?.Headers is not null)
        {
            var headers = KafkaTraceContext.ReadHeaders(result.Message.Headers);
            if (headers.TryGetValue(KafkaHeaderNames.EventType, out var headerEventType) &&
                !string.IsNullOrWhiteSpace(headerEventType))
            {
                eventType = headerEventType;
            }
        }

        _metrics.RecordConsumerError(topic, eventType, exception.GetType().Name);
    }

}
