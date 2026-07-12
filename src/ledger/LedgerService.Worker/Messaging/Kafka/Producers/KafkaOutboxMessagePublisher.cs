using System.Diagnostics;
using System.Text;

using Confluent.Kafka;

using LedgerService.Application.Abstractions.Messaging;
using LedgerService.Infrastructure.Observability;
using LedgerService.Worker.Messaging.Abstractions;
using LedgerService.Worker.Messaging.Kafka.Configuration;
using LedgerService.Worker.Messaging.Kafka.Tracing;

using Microsoft.Extensions.Options;

namespace LedgerService.Worker.Messaging.Kafka.Producers;

public sealed partial class KafkaOutboxMessagePublisher : IOutboxMessagePublisher, IDisposable
{
    private readonly KafkaProducerOptions _options;
    private readonly ILogger<KafkaOutboxMessagePublisher> _logger;
    private readonly OutboxMetrics _metrics;
    private readonly IProducer<string, string> _producer;

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Kafka producer error: {Reason} (IsFatal={IsFatal})")]
    private static partial void LogKafkaProducerError(ILogger logger, string reason, bool isFatal);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Kafka published outbox message {OutboxId} to {Topic} [partition={Partition}, offset={Offset}]")]
    private static partial void LogKafkaOutboxMessagePublished(
        ILogger logger,
        Guid outboxId,
        string topic,
        int partition,
        long offset);

    public KafkaOutboxMessagePublisher(
        IOptions<KafkaProducerOptions> options,
        ILogger<KafkaOutboxMessagePublisher> logger,
        OutboxMetrics metrics)
        : this(options, logger, metrics, producer: null)
    {
    }

    internal KafkaOutboxMessagePublisher(
        IOptions<KafkaProducerOptions> options,
        ILogger<KafkaOutboxMessagePublisher> logger,
        OutboxMetrics metrics,
        IProducer<string, string>? producer)
    {
        _options = options.Value;
        _logger = logger;
        _metrics = metrics;

        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = _options.ClientId,
            Acks = ParseAcks(_options.Acks),
            EnableIdempotence = _options.EnableIdempotence,
            MessageTimeoutMs = _options.MessageTimeoutMs
        };
        config.ApplySecurity(_options);

        _producer = producer ?? new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) =>
            {
                LogKafkaProducerError(_logger, e.Reason, e.IsFatal);
            })
            .Build();
    }

    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var topic = ResolveDestination(message);
        var key = message.AggregateId.ToString("N");

        var headers = new Headers
        {
            { KafkaHeaderNames.EventId, Encoding.UTF8.GetBytes(message.Id.ToString()) },
            { KafkaHeaderNames.EventType, Encoding.UTF8.GetBytes(message.EventType) }
        };
        if (message.CorrelationId is not null)
            headers.Add(KafkaHeaderNames.CorrelationId, Encoding.UTF8.GetBytes(message.CorrelationId.Value.ToString()));

        var activity = Activity.Current;
        KafkaTraceContext.AddPropagationHeaders(
            headers,
            message.TraceParent ?? activity?.Id,
            message.TraceState ?? activity?.TraceStateString,
            message.Baggage ?? KafkaTraceContext.FormatCurrentBaggage());

        var kafkaMessage = new Message<string, string>
        {
            Key = key,
            Value = message.Payload,
            Headers = headers,
            Timestamp = new Timestamp(message.OccurredAt)
        };

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var result = await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

            _metrics.RecordKafkaProducerMessagePublished(topic, message.EventType, "success");
            _metrics.RecordKafkaProducerPublishDuration(elapsedMilliseconds, topic, message.EventType, "success");

            LogKafkaOutboxMessagePublished(
                _logger,
                message.Id,
                topic,
                result.Partition.Value,
                result.Offset.Value);
        }
        catch (ProduceException<string, string> ex)
        {
            throw CreatePublishException(message, topic, startedAt, ex);
        }
        catch (KafkaException ex)
        {
            throw CreatePublishException(message, topic, startedAt, ex);
        }
        catch (TimeoutException ex)
        {
            throw CreatePublishException(message, topic, startedAt, ex);
        }
    }

    public string ResolveDestination(OutboxMessage message)
    {
        return _options.TopicMap.TryGetValue(message.EventType, out var mapped) && !string.IsNullOrWhiteSpace(mapped)
            ? mapped
            : _options.DefaultTopic;
    }

    private static Acks ParseAcks(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "0" => Acks.None,
            "1" => Acks.Leader,
            "all" => Acks.All,
            _ => Acks.All
        };
    }

    private MessagePublishException CreatePublishException(
        OutboxMessage message,
        string topic,
        long startedAt,
        Exception exception)
    {
        var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        var errorType = exception.GetType().Name;

        _metrics.RecordKafkaProducerMessagePublished(topic, message.EventType, "failure");
        _metrics.RecordKafkaProducerPublishDuration(elapsedMilliseconds, topic, message.EventType, "failure");
        _metrics.RecordKafkaProducerError(topic, message.EventType, errorType);

        return new MessagePublishException(
            $"Failed to publish outbox message {message.Id} to destination '{topic}'.",
            exception);
    }

    public void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
        }
        catch (KafkaException)
        {
            // ignore
        }

        _producer.Dispose();
    }
}
