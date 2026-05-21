using LedgerService.Api.Contracts.Requests;
using LedgerService.Api.Contracts.Responses;
using LedgerService.Application.Lancamentos.Queries;

namespace LedgerService.Api.Mappers;

public static class ObterStatusEstornoLancamentoMapper
{
    public static ObterStatusEstornoLancamentoResponse ToResponse(ObterStatusEstornoLancamentoResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new(
            result.EstornoId,
            result.LancamentoOriginalId,
            result.Status,
            result.Motivo,
            result.SolicitadoEm);
    }
}
