using MediatR;

namespace LedgerService.Application.Lancamentos.Queries;

public sealed record ObterStatusEstornoLancamentoQuery(
    Guid EstornoId,
    IReadOnlyCollection<string> AuthorizedMerchantIds) : IRequest<ObterStatusEstornoLancamentoResult>;
