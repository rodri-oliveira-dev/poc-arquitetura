using LedgerService.Application.Common.Exceptions;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;

using MediatR;

namespace LedgerService.Application.Lancamentos.Queries;

public sealed class ObterStatusReprocessamentoLancamentosHandler
    : IRequestHandler<ObterStatusReprocessamentoLancamentosQuery, ObterStatusReprocessamentoLancamentosResult>
{
    private readonly IReprocessamentoLancamentosRepository _reprocessamentoRepository;

    public ObterStatusReprocessamentoLancamentosHandler(
        IReprocessamentoLancamentosRepository reprocessamentoRepository)
    {
        _reprocessamentoRepository = reprocessamentoRepository;
    }

    public async Task<ObterStatusReprocessamentoLancamentosResult> Handle(
        ObterStatusReprocessamentoLancamentosQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reprocessamento = await _reprocessamentoRepository.GetByIdAsync(
            request.ReprocessamentoId,
            cancellationToken);

        if (reprocessamento is null)
            throw new NotFoundException("Solicitacao de reprocessamento nao encontrada.");

        if (!IsMerchantAuthorized(request.AuthorizedMerchantIds, reprocessamento.MerchantId))
            throw new ForbiddenException("Token sem autorizacao para o merchant do reprocessamento.");

        return ToResult(reprocessamento);
    }

    private static ObterStatusReprocessamentoLancamentosResult ToResult(
        ReprocessamentoLancamentos reprocessamento)
        => new(
            reprocessamento.Id,
            reprocessamento.MerchantId,
            reprocessamento.DataInicial,
            reprocessamento.DataFinal,
            reprocessamento.Status.ToString(),
            reprocessamento.Motivo,
            reprocessamento.CreatedAt);

    private static bool IsMerchantAuthorized(IReadOnlyCollection<string> authorizedMerchantIds, string merchantId)
        => authorizedMerchantIds.Any(value => string.Equals(value, merchantId, StringComparison.Ordinal));
}
