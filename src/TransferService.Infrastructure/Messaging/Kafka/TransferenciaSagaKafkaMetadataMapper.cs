using Microsoft.Extensions.Options;
using TransferService.Application.Transferencias.Events;

namespace TransferService.Infrastructure.Messaging.Kafka;

public sealed class TransferenciaSagaKafkaMetadataMapper
{
    private readonly TransferenciaKafkaTopicOptions _options;

    public TransferenciaSagaKafkaMetadataMapper(IOptions<TransferenciaKafkaTopicOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public TransferenciaKafkaPublicationMetadata Map(TransferenciaSagaEvent evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["event_type"] = evento.EventType,
            ["aggregate_type"] = "TransferenciaSaga",
            ["aggregate_id"] = evento.TransferenciaId.ToString()
        };

        if (!string.IsNullOrWhiteSpace(evento.CorrelationId))
            headers["correlation_id"] = evento.CorrelationId;

        return new(
            _options.ResolveTopic(evento.EventType),
            evento.TransferenciaId.ToString(),
            headers);
    }
}
