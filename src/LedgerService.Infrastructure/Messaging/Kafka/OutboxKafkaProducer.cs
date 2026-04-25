using System.Text;
using Confluent.Kafka;
using LedgerService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace LedgerService.Infrastructure.Messaging.Kafka;

public sealed class OutboxKafkaProducer : IOutboxEventProducer, IDisposable
{
    private readonly KafkaProducerOptions _options;
    private readonly ILogger<OutboxKafkaProducer> _logger;
    private readonly IProducer<string, string> _producer;

    public OutboxKafkaProducer(IOptions<KafkaProducerOptions> options, ILogger<OutboxKafkaProducer> logger)
    {
        _options = options.Value;
        _logger = logger;

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

    public async Task ProduceAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var topic = ResolveTopic(message);
        var key = message.AggregateId.ToString("N");

        var headers = new Headers();
        headers.Add("event_id", Encoding.UTF8.GetBytes(message.Id.ToString()));
        headers.Add("event_type", Encoding.UTF8.GetBytes(message.EventType));
        if (message.CorrelationId is not null)
            headers.Add("correlation_id", Encoding.UTF8.GetBytes(message.CorrelationId.Value.ToString()));

        // Propagação de contexto de trace (W3C) quando houver Activity atual.
        // - Não altera payload e é compatível com consumidores que ignoram headers desconhecidos.
        // - Quando não houver tracing habilitado, não adiciona headers.
        var activity = Activity.Current;
        if (activity is not null)
        {
            // 'traceparent' é o header W3C obrigatório.
            if (!string.IsNullOrWhiteSpace(activity.Id))
                headers.Add("traceparent", Encoding.UTF8.GetBytes(activity.Id));

            // 'tracestate' é opcional.
            if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
                headers.Add("tracestate", Encoding.UTF8.GetBytes(activity.TraceStateString));

            // 'baggage' é opcional.
            var baggage = string.Join(",", activity.Baggage.Select(kv => $"{kv.Key}={kv.Value}"));
            if (!string.IsNullOrWhiteSpace(baggage))
                headers.Add("baggage", Encoding.UTF8.GetBytes(baggage));
        }

        var kafkaMessage = new Message<string, string>
        {
            Key = key,
            Value = message.Payload,
            Headers = headers,
            Timestamp = new Timestamp(message.OccurredAt)
        };

        var result = await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);

        _logger.LogDebug(
            "Kafka published outbox message {OutboxId} to {Topic} [partition={Partition}, offset={Offset}]",
            message.Id,
            topic,
            result.Partition.Value,
            result.Offset.Value);
    }

    private string ResolveTopic(OutboxMessage message)
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
        catch
        {
            // ignore
        }

        _producer.Dispose();
    }
}
