using System.Diagnostics;
using System.Text;

using Confluent.Kafka;

using LedgerService.Domain.Entities;
using LedgerService.Infrastructure.Observability;
using LedgerService.Worker.Messaging.Abstractions;
using LedgerService.Worker.Messaging.Kafka.Configuration;
using LedgerService.Worker.Messaging.Kafka.Tracing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LedgerService.Worker.Messaging.Kafka.Producers;

public sealed class KafkaOutboxMessagePublisher : IOutboxMessagePublisher, IDisposable
{
    private readonly KafkaProducerOptions _options;
    private readonly ILogger<KafkaOutboxMessagePublisher> _logger;
    private readonly OutboxMetrics _metrics;
    private readonly IProducer<string, string> _producer;

    public KafkaOutboxMessagePublisher(
        IOptions<KafkaProducerOptions> options,
        ILogger<KafkaOutboxMessagePublisher> logger,
        OutboxMetrics metrics)
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

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) =>
            {
                _logger.LogWarning("Kafka producer error: {Reason} (IsFatal={IsFatal})", e.Reason, e.IsFatal);
            })
            .Build();
    }

    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var topic = ResolveDestination(message);
        var key = message.AggregateId.ToString("N");

        var headers = new Headers();
        headers.Add(KafkaHeaderNames.EventId, Encoding.UTF8.GetBytes(message.Id.ToString()));
        headers.Add(KafkaHeaderNames.EventType, Encoding.UTF8.GetBytes(message.EventType));
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

            _logger.LogDebug(
                "Kafka published outbox message {OutboxId} to {Topic} [partition={Partition}, offset={Offset}]",
                message.Id,
                topic,
                result.Partition.Value,
                result.Offset.Value);
        }
        catch (Exception ex) when (ex is ProduceException<string, string> or KafkaException or TimeoutException)
        {
            var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            var errorType = ex.GetType().Name;

            _metrics.RecordKafkaProducerMessagePublished(topic, message.EventType, "failure");
            _metrics.RecordKafkaProducerPublishDuration(elapsedMilliseconds, topic, message.EventType, "failure");
            _metrics.RecordKafkaProducerError(topic, message.EventType, errorType);

            throw;
        }
    }

    public string ResolveDestination(OutboxMessage message)
    {
        if (_options.TopicMap.TryGetValue(message.EventType, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
            return mapped;

        return _options.DefaultTopic;
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
