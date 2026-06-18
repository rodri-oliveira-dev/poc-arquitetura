using System.Text.Json;

using TransferService.Application.Abstractions.Messaging;
using TransferService.Application.Transferencias.Events;
using TransferService.Infrastructure.Messaging.Kafka;

namespace TransferService.Infrastructure.Persistence.Outbox;

public sealed class TransferenciaOutboxWriter : ITransferenciaOutboxWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TransferServiceDbContext _context;
    private readonly TransferenciaSagaKafkaMetadataMapper _metadataMapper;

    public TransferenciaOutboxWriter(
        TransferServiceDbContext context,
        TransferenciaSagaKafkaMetadataMapper metadataMapper)
    {
        _context = context;
        _metadataMapper = metadataMapper;
    }

    public async Task WriteAsync(TransferenciaSagaEvent evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);

        var metadata = _metadataMapper.Map(evento);
        var payload = JsonSerializer.Serialize(evento, evento.GetType(), JsonOptions);
        var message = new TransferenciaOutboxMessage(
            "TransferenciaSaga",
            evento.TransferenciaId,
            evento.EventType,
            payload,
            metadata.Topic,
            metadata.MessageKey,
            evento.CorrelationId,
            evento.OccurredAt,
            evento.OccurredAt);

        await _context.OutboxMessages.AddAsync(message, cancellationToken);
    }
}
