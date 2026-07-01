using System.Diagnostics.CodeAnalysis;

using AuditService.Worker.Messaging.Kafka.Configuration;

using Confluent.Kafka;

using Microsoft.Extensions.Options;

namespace AuditService.Worker.Messaging.Kafka;

internal sealed partial class AuditRecordRequestedConsumerService(
    IOptions<AuditRecordRequestedConsumerOptions> options,
    IAuditKafkaConsumerFactory consumerFactory,
    IAuditRecordRequestedProcessor processor,
    ILogger<AuditRecordRequestedConsumerService> logger) : BackgroundService
{
    private readonly AuditRecordRequestedConsumerOptions _options = options.Value;

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "BackgroundService must keep the consumer alive and retry processing failures without committing the offset.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateOptions(_options);

        using IAuditKafkaConsumer consumer = consumerFactory.Create();
        consumer.Subscribe(_options.Topic);
        LogConsumerStarted(logger, _options.GroupId, _options.ClientId, _options.Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeOnceAsync(consumer, stoppingToken);
            }
            catch (ConsumeException ex)
            {
                LogConsumeFailure(logger, ex);
                await Task.Delay(_options.ConsumeErrorRetryDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (KafkaException ex)
            {
                LogKafkaProcessingFailure(logger, ex);
                await Task.Delay(_options.ProcessingErrorRetryDelay, stoppingToken);
            }
            catch (Exception ex)
            {
                LogUnexpectedKafkaProcessingFailure(logger, ex);
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

        LogConsumerStopped(logger);
    }

    internal async Task<bool> ConsumeOnceAsync(
        IAuditKafkaConsumer consumer,
        CancellationToken cancellationToken)
    {
        ConsumeResult<string, string>? result = consumer.Consume(cancellationToken);
        if (result?.Message?.Value is null)
            return false;

        bool processed = await processor.ProcessAsync(result.Message.Value, cancellationToken);
        if (!processed)
            return false;

        consumer.Commit(result);
        return true;
    }

    internal static void ValidateOptions(AuditRecordRequestedConsumerOptions options)
    {
        if (!options.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(options.BootstrapServers))
            throw new InvalidOperationException("AuditRecordRequested Consumer BootstrapServers nao configurado.");

        if (string.IsNullOrWhiteSpace(options.GroupId))
            throw new InvalidOperationException("AuditRecordRequested Consumer GroupId nao configurado.");

        if (string.IsNullOrWhiteSpace(options.Topic))
            throw new InvalidOperationException("AuditRecordRequested Consumer Topic nao configurado.");

        if (options.ConsumeErrorRetryDelay <= TimeSpan.Zero)
            throw new InvalidOperationException("AuditRecordRequested Consumer ConsumeErrorRetryDelay deve ser maior que zero.");

        if (options.ProcessingErrorRetryDelay <= TimeSpan.Zero)
            throw new InvalidOperationException("AuditRecordRequested Consumer ProcessingErrorRetryDelay deve ser maior que zero.");
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Consumer AuditRecordRequested.v1 iniciado. groupId={GroupId} clientId={ClientId} topic={Topic}")]
    private static partial void LogConsumerStarted(ILogger logger, string groupId, string clientId, string topic);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Falha ao consumir AuditRecordRequested.v1.")]
    private static partial void LogConsumeFailure(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Falha Kafka ao processar AuditRecordRequested.v1.")]
    private static partial void LogKafkaProcessingFailure(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Falha inesperada ao processar AuditRecordRequested.v1.")]
    private static partial void LogUnexpectedKafkaProcessingFailure(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Consumer AuditRecordRequested.v1 parado.")]
    private static partial void LogConsumerStopped(ILogger logger);
}
