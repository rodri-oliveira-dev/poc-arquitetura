using System.Globalization;
using System.Text;
using System.Text.Json;

using Confluent.Kafka;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BalanceService.Infrastructure.Messaging.Kafka;

public sealed class KafkaDeadLetterProducer : IKafkaDeadLetterProducer, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly KafkaConsumerOptions _options;
    private readonly ILogger<KafkaDeadLetterProducer> _logger;
    private readonly IProducer<string, string> _producer;

    public KafkaDeadLetterProducer(IOptions<KafkaConsumerOptions> options, ILogger<KafkaDeadLetterProducer> logger)
    {
        _options = options.Value;
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = $"{_options.ClientId}-dlq",
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = _options.DeadLetterMessageTimeoutMs
        };
        config.ApplySecurity(_options);

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) =>
            {
                _logger.LogWarning("Kafka DLQ producer error: {Reason} (IsFatal={IsFatal})", e.Reason, e.IsFatal);
            })
            .Build();
    }

    public async Task ProduceAsync(DeadLetterMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.DeadLetterTopic))
            throw new InvalidOperationException("Kafka DeadLetterTopic não configurado.");

        var headers = new Headers
        {
            { "dlq_reason", Encoding.UTF8.GetBytes(message.Reason) },
            { "original_topic", Encoding.UTF8.GetBytes(message.OriginalTopic) },
            { "original_partition", Encoding.UTF8.GetBytes(message.OriginalPartition.ToString(CultureInfo.InvariantCulture)) },
            { "original_offset", Encoding.UTF8.GetBytes(message.OriginalOffset.ToString(CultureInfo.InvariantCulture)) }
        };

        CopyHeaderIfPresent(message.OriginalHeaders, headers, KafkaHeaderNames.EventType);
        CopyHeaderIfPresent(message.OriginalHeaders, headers, KafkaHeaderNames.EventId);
        CopyHeaderIfPresent(message.OriginalHeaders, headers, KafkaHeaderNames.CorrelationId);
        CopyHeaderIfPresent(message.OriginalHeaders, headers, KafkaHeaderNames.TraceParent);
        CopyHeaderIfPresent(message.OriginalHeaders, headers, KafkaHeaderNames.TraceState);
        CopyHeaderIfPresent(message.OriginalHeaders, headers, KafkaHeaderNames.Baggage);

        var result = await _producer.ProduceAsync(
            _options.DeadLetterTopic,
            new Message<string, string>
            {
                Key = $"{message.OriginalTopic}:{message.OriginalPartition}:{message.OriginalOffset}",
                Value = JsonSerializer.Serialize(message, JsonOptions),
                Headers = headers,
                Timestamp = new Timestamp(message.Timestamp.UtcDateTime)
            },
            cancellationToken);

        _logger.LogWarning(
            "Kafka message published to DLQ {DeadLetterTopic} [partition={Partition}, offset={Offset}] from {OriginalTopic}/{OriginalPartition}/{OriginalOffset}",
            _options.DeadLetterTopic,
            result.Partition.Value,
            result.Offset.Value,
            message.OriginalTopic,
            message.OriginalPartition,
            message.OriginalOffset);
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

    private static void CopyHeaderIfPresent(IReadOnlyDictionary<string, string> source, Headers target, string name)
    {
        if (source.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
            target.Add(name, Encoding.UTF8.GetBytes(value));
    }
}
