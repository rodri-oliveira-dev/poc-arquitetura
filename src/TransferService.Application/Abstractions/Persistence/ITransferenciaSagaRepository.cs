using TransferService.Domain.Sagas;

namespace TransferService.Application.Abstractions.Persistence;

public interface ITransferenciaSagaRepository
{
    Task<TransferenciaSaga?> GetByIdAsync(Guid transferenciaId, CancellationToken cancellationToken);

    Task AddAsync(TransferenciaSaga saga, CancellationToken cancellationToken);
}
