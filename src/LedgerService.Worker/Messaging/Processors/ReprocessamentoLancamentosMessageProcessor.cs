using System.Text.Json;

using LedgerService.Application.Lancamentos.Commands;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Worker.Messaging.Abstractions;

using MediatR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LedgerService.Worker.Messaging.Processors;

public sealed class ReprocessamentoLancamentosMessageProcessor
{
    public const string SourceName = "ledger.lancamentos.reprocessamento.solicitado";

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
        ReceivedMessage receivedMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = DeserializeAndValidate(receivedMessage);

            using var logScope = BeginProcessingLogScope(receivedMessage, message);
            await SendCommandAsync(message, cancellationToken);

            LogProcessedMessage(receivedMessage, message);

            return true;
        }
        catch (JsonException ex)
        {
            LogInvalidMessage(receivedMessage, "Payload invalido para ReprocessamentoLancamentosSolicitado.v1.", ex);
            return true;
        }
        catch (MessageValidationException ex)
        {
            LogInvalidMessage(receivedMessage, ex.Message, ex);
            return true;
        }
    }

    private static ReprocessamentoLancamentosSolicitadoV1 DeserializeAndValidate(ReceivedMessage receivedMessage)
    {
        ReprocessamentoLancamentosMessageValidator.ValidateEnvelope(receivedMessage);

        var message = JsonSerializer.Deserialize<ReprocessamentoLancamentosSolicitadoV1>(
            receivedMessage.Payload,
            JsonOptions);

        ReprocessamentoLancamentosMessageValidator.ValidatePayload(message);

        return message!;
    }

    private IDisposable? BeginProcessingLogScope(
        ReceivedMessage receivedMessage,
        ReprocessamentoLancamentosSolicitadoV1 message)
        => _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = message.CorrelationId,
            ["ReprocessamentoId"] = message.ReprocessamentoId,
            ["MerchantId"] = message.MerchantId,
            ["TransportProvider"] = receivedMessage.Transport.Provider,
            ["TransportSource"] = receivedMessage.Transport.Source,
            ["TransportPartition"] = receivedMessage.Transport.Partition,
            ["TransportOffset"] = receivedMessage.Transport.Offset,
            ["EventType"] = receivedMessage.EventType
        });

    private async Task SendCommandAsync(
        ReprocessamentoLancamentosSolicitadoV1 message,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(
            new ProcessarReprocessamentoLancamentosCommand(message.ReprocessamentoId),
            cancellationToken);
    }

    private void LogProcessedMessage(
        ReceivedMessage receivedMessage,
        ReprocessamentoLancamentosSolicitadoV1 message)
    {
        _logger.LogInformation(
            "Mensagem de reprocessamento processada. reprocessamentoId={ReprocessamentoId} provider={TransportProvider} source={TransportSource} partition={TransportPartition} offset={TransportOffset}",
            message.ReprocessamentoId,
            receivedMessage.Transport.Provider,
            receivedMessage.Transport.Source,
            receivedMessage.Transport.Partition,
            receivedMessage.Transport.Offset);
    }

    private void LogInvalidMessage(
        ReceivedMessage receivedMessage,
        string reason,
        Exception exception)
    {
        _logger.LogWarning(
            exception,
            "Mensagem de reprocessamento invalida ignorada. provider={TransportProvider} source={TransportSource} partition={TransportPartition} offset={TransportOffset} reason={Reason}",
            receivedMessage.Transport.Provider,
            receivedMessage.Transport.Source,
            receivedMessage.Transport.Partition,
            receivedMessage.Transport.Offset,
            reason);
    }

    private static class ReprocessamentoLancamentosMessageValidator
    {
        public static void ValidateEnvelope(ReceivedMessage receivedMessage)
        {
            ValidateSource(receivedMessage.Transport.Source);
            ValidateEventType(receivedMessage.EventType);
        }

        public static void ValidatePayload(ReprocessamentoLancamentosSolicitadoV1? message)
        {
            if (message is null)
                throw new MessageValidationException("Payload vazio ou invalido.");

            if (message.ReprocessamentoId == Guid.Empty)
                throw new MessageValidationException("Payload reprocessamentoId e obrigatorio.");

            if (string.IsNullOrWhiteSpace(message.MerchantId))
                throw new MessageValidationException("Payload merchantId e obrigatorio.");

            if (string.IsNullOrWhiteSpace(message.CorrelationId) || !Guid.TryParse(message.CorrelationId, out _))
                throw new MessageValidationException("Payload correlationId deve ser UUID valido.");
        }

        private static void ValidateSource(string source)
        {
            if (!string.Equals(source, SourceName, StringComparison.Ordinal))
                throw new MessageValidationException($"Fonte de transporte inesperada '{source}'.");
        }

        private static void ValidateEventType(string eventType)
        {
            if (string.IsNullOrWhiteSpace(eventType))
                throw new MessageValidationException("Atributo event_type e obrigatorio.");

            if (!string.Equals(eventType, ReprocessamentoLancamentosSolicitadoV1.EventType, StringComparison.Ordinal))
                throw new MessageValidationException($"Event_type nao suportado '{eventType}'.");
        }
    }

    private sealed class MessageValidationException : Exception
    {
        public MessageValidationException(string message) : base(message)
        {
        }
    }
}
