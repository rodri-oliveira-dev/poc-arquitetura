using System.Diagnostics;
using System.Text;
using System.Text.Json;

using BalanceService.Application.Balances.Commands;
using BalanceService.Domain.Balances;
using BalanceService.Domain.Exceptions;

using Confluent.Kafka;

using MediatR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BalanceService.Infrastructure.Messaging.Kafka;

public sealed class LedgerKafkaMessageProcessor
{
    private static readonly ActivitySource ActivitySource = new("BalanceService.KafkaConsumer");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceProvider _serviceProvider;
    private readonly IKafkaDeadLetterProducer _deadLetterProducer;
    private readonly ILogger<LedgerKafkaMessageProcessor> _logger;

    public LedgerKafkaMessageProcessor(
        IServiceProvider serviceProvider,
        IKafkaDeadLetterProducer deadLetterProducer,
        ILogger<LedgerKafkaMessageProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _deadLetterProducer = deadLetterProducer;
        _logger = logger;
    }

    public async Task<bool> ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
    {
        var headers = ReadHeaders(result.Message.Headers);

        try
        {
            ValidateEventType(headers);

            var deserialized = JsonSerializer.Deserialize<LedgerEntryCreatedEvent>(result.Message.Value, JsonOptions);
            ValidateEvent(deserialized);
            var evt = deserialized!;

            using var logScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["CorrelationId"] = evt.CorrelationId,
                ["EventId"] = ResolveEventId(headers, evt.Id),
                ["MerchantId"] = evt.MerchantId,
                ["OccurredAt"] = evt.OccurredAt,
                ["KafkaTopic"] = result.Topic,
                ["KafkaPartition"] = result.Partition.Value,
                ["KafkaOffset"] = result.Offset.Value,
                ["KafkaEventType"] = headers.GetValueOrDefault(KafkaHeaderNames.EventType)
            });

            using var activity = StartConsumerActivity(result, headers, evt);
            await ProcessMessageAsync(evt, cancellationToken);
            return true;
        }
        catch (JsonException ex)
        {
            await PublishToDeadLetterAsync(result, headers, "Deserialization failed.", ex, cancellationToken);
            return true;
        }
        catch (KafkaMessageValidationException ex)
        {
            await PublishToDeadLetterAsync(result, headers, ex.Message, ex, cancellationToken);
            return true;
        }
        catch (DomainException ex)
        {
            await PublishToDeadLetterAsync(result, headers, "Non-recoverable processing failure.", ex, cancellationToken);
            return true;
        }
        catch (ArgumentException ex)
        {
            await PublishToDeadLetterAsync(result, headers, "Non-recoverable processing failure.", ex, cancellationToken);
            return true;
        }
        catch (InvalidOperationException ex) when (IsNonRecoverableProcessingFailure(ex))
        {
            await PublishToDeadLetterAsync(result, headers, "Non-recoverable processing failure.", ex, cancellationToken);
            return true;
        }
    }

    private async Task ProcessMessageAsync(LedgerEntryCreatedEvent evt, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new ApplyLedgerEntryCreatedCommand(evt), ct);
    }

    private async Task PublishToDeadLetterAsync(
        ConsumeResult<string, string> result,
        IReadOnlyDictionary<string, string> headers,
        string reason,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var message = new DeadLetterMessage(
            result.Message.Value,
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            headers,
            reason,
            exception.GetType().Name,
            DateTimeOffset.UtcNow);

        await _deadLetterProducer.ProduceAsync(message, cancellationToken);

        _logger.LogWarning(
            exception,
            "Mensagem enviada para DLQ. topic={Topic} partition={Partition} offset={Offset} reason={Reason}",
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            reason);
    }

    private static Activity? StartConsumerActivity(
        ConsumeResult<string, string> result,
        IReadOnlyDictionary<string, string> headers,
        LedgerEntryCreatedEvent evt)
    {
        ActivityContext parentContext = default;
        if (headers.TryGetValue(KafkaHeaderNames.TraceParent, out var traceParent))
            ActivityContext.TryParse(traceParent, headers.GetValueOrDefault(KafkaHeaderNames.TraceState), out parentContext);

        var activity = parentContext == default
            ? ActivitySource.StartActivity("kafka.consume", ActivityKind.Consumer)
            : ActivitySource.StartActivity("kafka.consume", ActivityKind.Consumer, parentContext);

        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.operation", "consume");
        activity?.SetTag("messaging.destination", result.Topic);
        activity?.SetTag("messaging.kafka.partition", result.Partition.Value);
        activity?.SetTag("messaging.kafka.offset", result.Offset.Value);
        activity?.SetTag("correlation_id", evt.CorrelationId);
        activity?.SetTag("event_id", ResolveEventId(headers, evt.Id));
        activity?.SetTag("event_type", headers.GetValueOrDefault(KafkaHeaderNames.EventType));
        activity?.AddBaggage("correlation_id", evt.CorrelationId);

        if (headers.TryGetValue(KafkaHeaderNames.Baggage, out var baggage))
            activity?.SetTag(KafkaHeaderNames.Baggage, baggage);

        return activity;
    }

    private static void ValidateEventType(IReadOnlyDictionary<string, string> headers)
    {
        if (!headers.TryGetValue(KafkaHeaderNames.EventType, out var eventType) || string.IsNullOrWhiteSpace(eventType))
            throw new KafkaMessageValidationException("Missing required Kafka header event_type.");

        if (!string.Equals(eventType, LedgerEntryCreatedV1Contract.EventType, StringComparison.Ordinal))
            throw new KafkaMessageValidationException($"Unsupported Kafka event_type '{eventType}'.");
    }

    private static void ValidateEvent(LedgerEntryCreatedEvent? evt)
    {
        if (evt is null)
            throw new KafkaMessageValidationException("Message payload is empty or invalid.");

        if (string.IsNullOrWhiteSpace(evt.Id))
            throw new KafkaMessageValidationException("Message payload id is required.");

        if (string.IsNullOrWhiteSpace(evt.Type))
            throw new KafkaMessageValidationException("Message payload type is required.");

        if (string.IsNullOrWhiteSpace(evt.Amount))
            throw new KafkaMessageValidationException("Message payload amount is required.");

        if (string.IsNullOrWhiteSpace(evt.MerchantId))
            throw new KafkaMessageValidationException("Message payload merchantId is required.");

        if (string.IsNullOrWhiteSpace(evt.CorrelationId))
            throw new KafkaMessageValidationException("Message payload correlationId is required.");
    }

    private static string ResolveEventId(IReadOnlyDictionary<string, string> headers, string payloadEventId)
        => headers.TryGetValue(KafkaHeaderNames.EventId, out var eventId) && !string.IsNullOrWhiteSpace(eventId)
            ? eventId
            : payloadEventId;

    private static IReadOnlyDictionary<string, string> ReadHeaders(Headers? headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headers is null)
            return result;

        foreach (var header in headers)
            result[header.Key] = Encoding.UTF8.GetString(header.GetValueBytes());

        return result;
    }

    private static bool IsNonRecoverableProcessingFailure(InvalidOperationException ex)
        => ex.Source?.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase) != true;

    private sealed class KafkaMessageValidationException : Exception
    {
        public KafkaMessageValidationException(string message) : base(message)
        {
        }
    }
}
