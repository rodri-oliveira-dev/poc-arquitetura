using System.Diagnostics.Metrics;

using LedgerService.Domain.Entities;
using LedgerService.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerService.Infrastructure.Observability;

public sealed class OutboxMetrics : IDisposable
{
    public const string MeterName = "LedgerService.Outbox";
    public const string MessagesCreatedMetricName = "ledger.outbox.messages.created";
    public const string MessagesPublishedMetricName = "ledger.outbox.messages.published";
    public const string PublishDurationMetricName = "ledger.outbox.publish.duration";
    public const string MessagesPendingMetricName = "ledger.outbox.messages.pending";
    public const string MessagesFailedMetricName = "ledger.outbox.messages.failed";
    public const string PublishAttemptsMetricName = "ledger.outbox.publish.attempts";
    public const string KafkaProducerMessagesPublishedMetricName = "ledger.kafka.producer.messages.published";
    public const string KafkaProducerPublishDurationMetricName = "ledger.kafka.producer.publish.duration";
    public const string KafkaProducerErrorsMetricName = "ledger.kafka.producer.errors";

    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly Meter _meter;
    private readonly Counter<long> _messagesCreated;
    private readonly Counter<long> _messagesPublished;
    private readonly Histogram<double> _publishDuration;
    private readonly Counter<long> _publishAttempts;
    private readonly Counter<long> _kafkaProducerMessagesPublished;
    private readonly Histogram<double> _kafkaProducerPublishDuration;
    private readonly Counter<long> _kafkaProducerErrors;

    public OutboxMetrics(IServiceScopeFactory scopeFactory)
        : this(MeterName, scopeFactory)
    {
    }

    public OutboxMetrics(string meterName, IServiceScopeFactory? scopeFactory = null)
    {
        _scopeFactory = scopeFactory;
        _meter = new Meter(meterName);
        _messagesCreated = _meter.CreateCounter<long>(
            MessagesCreatedMetricName,
            unit: "1",
            description: "Total de mensagens criadas na Outbox do Ledger.");
        _messagesPublished = _meter.CreateCounter<long>(
            MessagesPublishedMetricName,
            unit: "1",
            description: "Total de mensagens Outbox publicadas ou tentadas no Kafka por resultado.");
        _publishDuration = _meter.CreateHistogram<double>(
            PublishDurationMetricName,
            unit: "ms",
            description: "Duracao da operacao tecnica de publicacao de uma mensagem Outbox no Kafka.");
        _publishAttempts = _meter.CreateCounter<long>(
            PublishAttemptsMetricName,
            unit: "1",
            description: "Total de tentativas tecnicas de publicacao de mensagens Outbox no Kafka.");
        _meter.CreateObservableGauge(
            MessagesPendingMetricName,
            () => ObserveOutboxStatus(OutboxStatus.Pending),
            unit: "1",
            description: "Quantidade atual de mensagens pendentes na Outbox por tipo de evento.");
        _meter.CreateObservableGauge(
            MessagesFailedMetricName,
            () => ObserveOutboxStatus(OutboxStatus.Failed),
            unit: "1",
            description: "Quantidade atual de mensagens failed na Outbox por tipo de evento.");
        _kafkaProducerMessagesPublished = _meter.CreateCounter<long>(
            KafkaProducerMessagesPublishedMetricName,
            unit: "1",
            description: "Total de mensagens publicadas ou tentadas pelo producer Kafka do Ledger por resultado.");
        _kafkaProducerPublishDuration = _meter.CreateHistogram<double>(
            KafkaProducerPublishDurationMetricName,
            unit: "ms",
            description: "Duracao da chamada de publicacao do producer Kafka do Ledger.");
        _kafkaProducerErrors = _meter.CreateCounter<long>(
            KafkaProducerErrorsMetricName,
            unit: "1",
            description: "Total de erros do producer Kafka do Ledger por tipo estavel de erro.");
    }

    public void RecordMessageCreated(string eventType)
    {
        _messagesCreated.Add(
            1,
            new KeyValuePair<string, object?>("event_type", eventType));
    }

    public void RecordOutboxMessagePublished(string eventType, string topic, string result)
    {
        _messagesPublished.Add(
            1,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("topic", topic),
            new KeyValuePair<string, object?>("result", result));
    }

    public void RecordOutboxPublishDuration(double elapsedMilliseconds, string eventType, string topic, string result)
    {
        _publishDuration.Record(
            elapsedMilliseconds,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("topic", topic),
            new KeyValuePair<string, object?>("result", result));
    }

    public void RecordPublishAttempt(string eventType, string result)
    {
        _publishAttempts.Add(
            1,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("result", result));
    }

    public void RecordKafkaProducerMessagePublished(string topic, string eventType, string result)
    {
        _kafkaProducerMessagesPublished.Add(
            1,
            new KeyValuePair<string, object?>("topic", topic),
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("result", result));
    }

    public void RecordKafkaProducerPublishDuration(double elapsedMilliseconds, string topic, string eventType, string result)
    {
        _kafkaProducerPublishDuration.Record(
            elapsedMilliseconds,
            new KeyValuePair<string, object?>("topic", topic),
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("result", result));
    }

    public void RecordKafkaProducerError(string topic, string eventType, string errorType)
    {
        _kafkaProducerErrors.Add(
            1,
            new KeyValuePair<string, object?>("topic", topic),
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("error_type", errorType));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private IEnumerable<Measurement<long>> ObserveOutboxStatus(OutboxStatus status)
    {
        if (_scopeFactory is null)
            return Array.Empty<Measurement<long>>();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var counts = db.OutboxMessages
                .AsNoTracking()
                .Where(message => message.Status == status)
                .GroupBy(message => message.EventType)
                .Select(group => new
                {
                    EventType = group.Key,
                    Count = group.LongCount()
                })
                .ToArray();

            return counts.Select(count => new Measurement<long>(
                count.Count,
                new KeyValuePair<string, object?>("event_type", count.EventType)));
        }
        catch (Exception)
        {
            return Array.Empty<Measurement<long>>();
        }
    }
}
