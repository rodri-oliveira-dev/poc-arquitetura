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
            ValidateSource(receivedMessage.Transport.Source);
            ValidateEventType(receivedMessage.EventType);

            var message = JsonSerializer.Deserialize<ReprocessamentoLancamentosSolicitadoV1>(
                receivedMessage.Payload,
                JsonOptions);
            ValidateMessage(message);

            using var logScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["CorrelationId"] = message!.CorrelationId,
                ["ReprocessamentoId"] = message.ReprocessamentoId,
                ["MerchantId"] = message.MerchantId,
                ["TransportProvider"] = receivedMessage.Transport.Provider,
                ["TransportSource"] = receivedMessage.Transport.Source,
                ["TransportPartition"] = receivedMessage.Transport.Partition,
                ["TransportOffset"] = receivedMessage.Transport.Offset,
                ["EventType"] = receivedMessage.EventType
            });

            using var scope = _serviceProvider.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(
                new ProcessarReprocessamentoLancamentosCommand(message.ReprocessamentoId),
                cancellationToken);

            _logger.LogInformation(
                "Mensagem de reprocessamento processada. reprocessamentoId={ReprocessamentoId} provider={TransportProvider} source={TransportSource} partition={TransportPartition} offset={TransportOffset}",
                message.ReprocessamentoId,
                receivedMessage.Transport.Provider,
                receivedMessage.Transport.Source,
                receivedMessage.Transport.Partition,
                receivedMessage.Transport.Offset);

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

    private static void ValidateMessage(ReprocessamentoLancamentosSolicitadoV1? message)
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

    private sealed class MessageValidationException : Exception
    {
        public MessageValidationException(string message) : base(message)
        {
        }
    }
}
