using MediatR;

using TransferService.Application.Abstractions.Persistence;
using TransferService.Application.Common.Exceptions;
using TransferService.Domain.Sagas;

namespace TransferService.Application.Transferencias.Queries;

public sealed class ObterStatusTransferenciaQueryHandler
    : IRequestHandler<ObterStatusTransferenciaQuery, ObterStatusTransferenciaResult>
{
    private readonly ITransferenciaSagaRepository _sagaRepository;

    public ObterStatusTransferenciaQueryHandler(ITransferenciaSagaRepository sagaRepository)
    {
        _sagaRepository = sagaRepository;
    }

    public async Task<ObterStatusTransferenciaResult> Handle(
        ObterStatusTransferenciaQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var saga = await _sagaRepository.GetByIdAsync(request.TransferenciaId, cancellationToken);
        if (saga is null)
            throw new NotFoundException("Transferencia nao encontrada.");

        if (!IsMerchantAuthorized(request.AuthorizedMerchantIds, saga))
            throw new ForbiddenException("Token sem autorizacao para os merchants da transferencia.");

        return ToResult(saga);
    }

    private static ObterStatusTransferenciaResult ToResult(TransferenciaSaga saga)
        => new(
            saga.Id,
            saga.Status.ToString(),
            saga.SourceMerchantId.Value,
            saga.DestinationMerchantId.Value,
            saga.Amount.Value,
            saga.CreatedAt,
            saga.UpdatedAt);

    private static bool IsMerchantAuthorized(
        IReadOnlyCollection<string> authorizedMerchantIds,
        TransferenciaSaga saga)
        => authorizedMerchantIds.Any(value =>
            string.Equals(value, saga.SourceMerchantId.Value, StringComparison.Ordinal)
            || string.Equals(value, saga.DestinationMerchantId.Value, StringComparison.Ordinal));
}
