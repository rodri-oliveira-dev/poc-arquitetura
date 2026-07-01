using MediatR;

namespace TransferService.Application.Transferencias.Queries;

public sealed record ObterStatusTransferenciaQuery(
    Guid TransferenciaId,
    IReadOnlyCollection<string> AuthorizedMerchantIds) : IRequest<ObterStatusTransferenciaResult>;
