using LedgerService.Application.Common.Exceptions;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using MediatR;

namespace LedgerService.Application.Lancamentos.Queries;

public sealed class ObterStatusEstornoLancamentoHandler
    : IRequestHandler<ObterStatusEstornoLancamentoQuery, ObterStatusEstornoLancamentoResult>
{
    private readonly IEstornoLancamentoRepository _estornoRepository;

    public ObterStatusEstornoLancamentoHandler(IEstornoLancamentoRepository estornoRepository)
    {
        _estornoRepository = estornoRepository;
    }

    public async Task<ObterStatusEstornoLancamentoResult> Handle(
        ObterStatusEstornoLancamentoQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var estorno = await _estornoRepository.GetByIdAsync(request.EstornoId, cancellationToken);
        if (estorno is null)
            throw new NotFoundException("Solicitacao de estorno nao encontrada.");

        if (!IsMerchantAuthorized(request.AuthorizedMerchantIds, estorno.MerchantId))
            throw new ForbiddenException("Token sem autorizacao para o merchant do estorno.");

        return ToResult(estorno);
    }

    private static ObterStatusEstornoLancamentoResult ToResult(EstornoLancamento estorno)
        => new(
            estorno.Id,
            estorno.LancamentoOriginalId,
            estorno.Status.ToString(),
            estorno.Motivo,
            estorno.CreatedAt);

    private static bool IsMerchantAuthorized(IReadOnlyCollection<string> authorizedMerchantIds, string merchantId)
        => authorizedMerchantIds.Any(value => string.Equals(value, merchantId, StringComparison.Ordinal));
}
