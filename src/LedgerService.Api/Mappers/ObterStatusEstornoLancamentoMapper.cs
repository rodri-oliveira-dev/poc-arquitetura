using LedgerService.Api.Contracts;
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
