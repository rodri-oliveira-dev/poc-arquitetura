using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using AuditService.Worker.Messaging.Kafka.Configuration;
using AuditService.Worker.Observability;

using Confluent.Kafka;

using Microsoft.Extensions.Options;

namespace AuditService.Worker.Messaging.Kafka;

internal sealed partial class AuditRecordRequestedConsumerService(
    IOptions<AuditRecordRequestedConsumerOptions> options,
    IAuditKafkaConsumerFactory consumerFactory,
    IAuditRecordRequestedProcessor processor,
    AuditWorkerMetrics metrics,
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

        var receivedMessage = new AuditKafkaReceivedMessage(
            result.Message.Value,
            result.Topic,
            result.Partition.Value,
            result.Offset.Value);
        long startedAt = Stopwatch.GetTimestamp();

        AuditRecordRequestedProcessingResult processingResult = await ProcessWithRetryAsync(receivedMessage, cancellationToken);
        metrics.RecordConsumerMessage(receivedMessage.Topic, processingResult.Result);
        metrics.RecordConsumerProcessingDuration(
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            receivedMessage.Topic,
            processingResult.Result);

        if (!processingResult.ShouldCommit)
            return false;

        consumer.Commit(result);
        return true;
    }

    private async Task<AuditRecordRequestedProcessingResult> ProcessWithRetryAsync(
        AuditKafkaReceivedMessage message,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _options.MaxProcessingAttempts; attempt++)
        {
            try
            {
                return await processor.ProcessAsync(message, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < _options.MaxProcessingAttempts)
            {
                metrics.RecordConsumerRetry(message.Topic, ex.GetType().Name);
                LogAuditRecordRequestedRetryScheduled(
                    logger,
                    ex,
                    new AuditRecordRetryContext(
                        message.Topic,
                        message.Partition,
                        message.Offset,
                        attempt,
                        _options.MaxProcessingAttempts,
                        _options.ProcessingRetryDelay));

                await Task.Delay(_options.ProcessingRetryDelay, cancellationToken);
            }
        }

        throw new InvalidOperationException("AuditRecordRequested Consumer retry loop completed without processing result.");
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

        if (string.IsNullOrWhiteSpace(options.DeadLetterTopic))
            throw new InvalidOperationException("AuditRecordRequested Consumer DeadLetterTopic nao configurado.");

        if (options.DeadLetterMessageTimeoutMs <= 0)
            throw new InvalidOperationException("AuditRecordRequested Consumer DeadLetterMessageTimeoutMs deve ser maior que zero.");

        if (options.MaxProcessingAttempts <= 0)
            throw new InvalidOperationException("AuditRecordRequested Consumer MaxProcessingAttempts deve ser maior que zero.");

        if (options.ProcessingRetryDelay <= TimeSpan.Zero)
            throw new InvalidOperationException("AuditRecordRequested Consumer ProcessingRetryDelay deve ser maior que zero.");

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

    private static readonly Action<ILogger, string, int, long, int, int, TimeSpan, Exception?> AuditRecordRequestedRetryScheduled =
        LoggerMessage.Define<string, int, long, int, int, TimeSpan>(
            LogLevel.Warning,
            new EventId(6, nameof(LogAuditRecordRequestedRetryScheduled)),
            "Retry local agendado para AuditRecordRequested.v1. topic={Topic} partition={Partition} offset={Offset} attempt={Attempt} maxAttempts={MaxAttempts} delay={Delay}");

    private static void LogAuditRecordRequestedRetryScheduled(
        ILogger logger,
        Exception exception,
        AuditRecordRetryContext context)
        => AuditRecordRequestedRetryScheduled(
            logger,
            context.Topic,
            context.Partition,
            context.Offset,
            context.Attempt,
            context.MaxAttempts,
            context.Delay,
            exception);

    private sealed record AuditRecordRetryContext(
        string Topic,
        int Partition,
        long Offset,
        int Attempt,
        int MaxAttempts,
        TimeSpan Delay);
}
