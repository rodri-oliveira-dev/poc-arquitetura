using System.Text;
using System.Text.Json;

using Confluent.Kafka;

using LedgerService.Application.Lancamentos.Commands;
using LedgerService.Application.Lancamentos.Events;

using MediatR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LedgerService.Worker.Messaging.Kafka.Processors;

public sealed class ReprocessamentoLancamentosMessageProcessor
{
    public const string TopicName = "ledger.lancamentos.reprocessamento.solicitado";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReprocessamentoLancamentosMessageProcessor> _logger;

    public ReprocessamentoLancamentosMessageProcessor(
        IServiceProvider serviceProvider,
        ILogger<ReprocessamentoLancamentosMessageProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<bool> ProcessAsync(
        ConsumeResult<string, string> result,
        CancellationToken cancellationToken)
    {
        var headers = ReadHeaders(result.Message.Headers);

        try
        {
            ValidateTopic(result.Topic);
            ValidateEventType(headers);

            var message = JsonSerializer.Deserialize<ReprocessamentoLancamentosSolicitadoV1>(
                result.Message.Value,
                JsonOptions);
            ValidateMessage(message);

            using var logScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["CorrelationId"] = message!.CorrelationId,
                ["ReprocessamentoId"] = message.ReprocessamentoId,
                ["MerchantId"] = message.MerchantId,
                ["KafkaTopic"] = result.Topic,
                ["KafkaPartition"] = result.Partition.Value,
                ["KafkaOffset"] = result.Offset.Value,
                ["KafkaEventType"] = headers.GetValueOrDefault("event_type")
            });

            using var scope = _serviceProvider.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(
                new ProcessarReprocessamentoLancamentosCommand(message.ReprocessamentoId),
                cancellationToken);

            _logger.LogInformation(
                "Mensagem de reprocessamento processada. reprocessamentoId={ReprocessamentoId} topic={Topic} partition={Partition} offset={Offset}",
                message.ReprocessamentoId,
                result.Topic,
                result.Partition.Value,
                result.Offset.Value);

            return true;
        }
        catch (JsonException ex)
        {
            LogInvalidMessage(result, "Payload invalido para ReprocessamentoLancamentosSolicitado.v1.", ex);
            return true;
        }
        catch (KafkaMessageValidationException ex)
        {
            LogInvalidMessage(result, ex.Message, ex);
            return true;
        }
    }

    private static void ValidateTopic(string topic)
    {
        if (!string.Equals(topic, TopicName, StringComparison.Ordinal))
            throw new KafkaMessageValidationException($"Topico Kafka inesperado '{topic}'.");
    }

    private static void ValidateEventType(IReadOnlyDictionary<string, string> headers)
    {
        if (!headers.TryGetValue("event_type", out var eventType) || string.IsNullOrWhiteSpace(eventType))
            throw new KafkaMessageValidationException("Header Kafka event_type e obrigatorio.");

        if (!string.Equals(eventType, ReprocessamentoLancamentosSolicitadoV1.EventType, StringComparison.Ordinal))
            throw new KafkaMessageValidationException($"Kafka event_type nao suportado '{eventType}'.");
    }

    private static void ValidateMessage(ReprocessamentoLancamentosSolicitadoV1? message)
    {
        if (message is null)
            throw new KafkaMessageValidationException("Payload vazio ou invalido.");

        if (message.ReprocessamentoId == Guid.Empty)
            throw new KafkaMessageValidationException("Payload reprocessamentoId e obrigatorio.");

        if (string.IsNullOrWhiteSpace(message.MerchantId))
            throw new KafkaMessageValidationException("Payload merchantId e obrigatorio.");

        if (string.IsNullOrWhiteSpace(message.CorrelationId) || !Guid.TryParse(message.CorrelationId, out _))
            throw new KafkaMessageValidationException("Payload correlationId deve ser UUID valido.");
    }

    private void LogInvalidMessage(
        ConsumeResult<string, string> result,
        string reason,
        Exception exception)
    {
        _logger.LogWarning(
            exception,
            "Mensagem de reprocessamento invalida ignorada. topic={Topic} partition={Partition} offset={Offset} reason={Reason}",
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            reason);
    }

    private static IReadOnlyDictionary<string, string> ReadHeaders(Headers? headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headers is null)
            return result;

        foreach (var header in headers)
            result[header.Key] = Encoding.UTF8.GetString(header.GetValueBytes());

        return result;
    }

    private sealed class KafkaMessageValidationException : Exception
    {
        public KafkaMessageValidationException(string message) : base(message)
        {
        }
    }
}
