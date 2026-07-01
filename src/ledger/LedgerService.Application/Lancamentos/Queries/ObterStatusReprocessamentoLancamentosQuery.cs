using MediatR;

namespace LedgerService.Application.Lancamentos.Queries;

public sealed record ObterStatusReprocessamentoLancamentosQuery(
    Guid ReprocessamentoId,
    IReadOnlyCollection<string> AuthorizedMerchantIds) : IRequest<ObterStatusReprocessamentoLancamentosResult>;
