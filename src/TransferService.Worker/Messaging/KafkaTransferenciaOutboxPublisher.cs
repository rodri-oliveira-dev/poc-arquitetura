using System.Text;
using System.Text.Json;

using Confluent.Kafka;

using Microsoft.Extensions.Options;

using TransferService.Infrastructure.Persistence.Outbox;
using TransferService.Worker.Options;

namespace TransferService.Worker.Messaging;

public sealed class KafkaTransferenciaOutboxPublisher : ITransferenciaKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaTransferenciaOutboxPublisher> _logger;

    public KafkaTransferenciaOutboxPublisher(
        IOptions<TransferWorkerOptions> options,
        ILogger<KafkaTransferenciaOutboxPublisher> logger)
        : this(options, logger, producer: null)
    {
    }

    internal KafkaTransferenciaOutboxPublisher(
        IOptions<TransferWorkerOptions> options,
        ILogger<KafkaTransferenciaOutboxPublisher> logger,
        IProducer<string, string>? producer)
    {
        _logger = logger;
        var kafka = options.Value.Kafka;
        var config = new ProducerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            ClientId = kafka.ClientId,
            Acks = ParseAcks(kafka.Acks),
            EnableIdempotence = kafka.EnableIdempotence,
            MessageTimeoutMs = kafka.MessageTimeoutMs
        };

        if (Enum.TryParse<SecurityProtocol>(kafka.SecurityProtocol, ignoreCase: true, out var securityProtocol))
            config.SecurityProtocol = securityProtocol;

        _producer = producer ?? new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, error) =>
                _logger.LogWarning("Kafka producer error: {Reason} (IsFatal={IsFatal})", error.Reason, error.IsFatal))
            .Build();
    }

    public Task PublishAsync(TransferenciaOutboxMessage message, string topic, CancellationToken cancellationToken)
        => ProduceAsync(message, topic, message.Payload, message.MessageKey, dlqReason: null, cancellationToken);

    public Task PublishDlqAsync(TransferenciaOutboxMessage message, string reason, string dlqTopic, CancellationToken cancellationToken)
    {
        var originalPayload = JsonSerializer.Serialize(message.Payload);
        var payload = $$"""
        {
          "outboxId": "{{message.Id}}",
          "eventType": "{{message.EventType}}",
          "aggregateId": "{{message.AggregateId}}",
          "correlationId": "{{message.CorrelationId}}",
          "originalTopic": "{{message.Topic}}",
          "reason": "{{JsonEncodedText.Encode(reason)}}",
          "payload": {{originalPayload}}
        }
        """;

        return ProduceAsync(message, dlqTopic, payload, message.MessageKey, reason, cancellationToken);
    }

    private async Task ProduceAsync(
        TransferenciaOutboxMessage message,
        string topic,
        string payload,
        string key,
        string? dlqReason,
        CancellationToken cancellationToken)
    {
        var headers = new Headers
        {
            { "event_id", Encoding.UTF8.GetBytes(message.Id.ToString()) },
            { "event_type", Encoding.UTF8.GetBytes(message.EventType) },
            { "aggregate_type", Encoding.UTF8.GetBytes(message.AggregateType) },
            { "aggregate_id", Encoding.UTF8.GetBytes(message.AggregateId.ToString()) }
        };

        if (!string.IsNullOrWhiteSpace(message.CorrelationId))
            headers.Add("correlation_id", Encoding.UTF8.GetBytes(message.CorrelationId));
        if (!string.IsNullOrWhiteSpace(dlqReason))
            headers.Add("dlq_reason", Encoding.UTF8.GetBytes(dlqReason));

        try
        {
            await _producer.ProduceAsync(
                topic,
                new Message<string, string>
                {
                    Key = key,
                    Value = payload,
                    Headers = headers,
                    Timestamp = new Timestamp(message.OccurredAt.UtcDateTime)
                },
                cancellationToken);
        }
        catch (ProduceException<string, string> ex)
        {
            throw new TransferenciaKafkaPublishException(
                $"Falha ao publicar Outbox {message.Id} no topico '{topic}'.",
                IsTransient(ex.Error),
                ex);
        }
        catch (KafkaException ex)
        {
            throw new TransferenciaKafkaPublishException(
                $"Falha Kafka ao publicar Outbox {message.Id} no topico '{topic}'.",
                !ex.Error.IsFatal,
                ex);
        }
        catch (TimeoutException ex)
        {
            throw new TransferenciaKafkaPublishException(
                $"Timeout ao publicar Outbox {message.Id} no topico '{topic}'.",
                isTransient: true,
                ex);
        }
    }

    private static bool IsTransient(Error error)
        => !error.IsFatal && !error.IsLocalError;

    private static Acks ParseAcks(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "0" => Acks.None,
            "1" => Acks.Leader,
            "all" => Acks.All,
            _ => Acks.All
        };

    public void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
        }
        catch (KafkaException)
        {
            // best effort shutdown
        }

        _producer.Dispose();
    }
}
