using Confluent.Kafka;

using BalanceService.Infrastructure.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BalanceService.Infrastructure.Messaging.Kafka;

public sealed class LedgerEventsConsumer : BackgroundService
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
                if (result?.Message?.Value is null)
                    continue;

                if (await _messageProcessor.ProcessAsync(result, stoppingToken))
                {
                    consumer.Commit(result);
                    _logger.KafkaMessageCommitted();
                }
            }
            catch (ConsumeException ex)
            {
                _logger.KafkaConsumeError(ex);
                await Task.Delay(_options.ConsumeErrorRetryDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (DbUpdateException ex)
            {
                _logger.KafkaProcessingError(ex, result?.Topic, result?.Partition.Value, result?.Offset.Value);

                await Task.Delay(_options.ProcessingErrorRetryDelay, stoppingToken);
            }
            catch (TimeoutException ex)
            {
                _logger.KafkaProcessingError(ex, result?.Topic, result?.Partition.Value, result?.Offset.Value);

                await Task.Delay(_options.ProcessingErrorRetryDelay, stoppingToken);
            }
            catch (KafkaException ex)
            {
                _logger.KafkaProcessingErrorWithKafkaException(ex, result?.Topic, result?.Partition.Value, result?.Offset.Value);

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

        _logger.ConsumerStopped();
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

}
