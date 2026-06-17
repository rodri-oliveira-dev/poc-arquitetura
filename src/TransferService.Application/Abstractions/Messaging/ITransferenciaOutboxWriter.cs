using TransferService.Application.Transferencias.Events;

namespace TransferService.Application.Abstractions.Messaging;

public interface ITransferenciaOutboxWriter
{
    Task WriteAsync(TransferenciaSagaEvent evento, CancellationToken cancellationToken);
}
