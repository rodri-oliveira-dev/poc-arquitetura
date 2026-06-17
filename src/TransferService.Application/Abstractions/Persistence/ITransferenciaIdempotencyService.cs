using TransferService.Application.Transferencias.Commands;

namespace TransferService.Application.Abstractions.Persistence;

public interface ITransferenciaIdempotencyService
{
    Task<TransferenciaIdempotencyEntry?> GetAsync(
        string sourceMerchantId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task AddAsync(
        string sourceMerchantId,
        string idempotencyKey,
        string requestHash,
        SolicitarTransferenciaResult response,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);
}
