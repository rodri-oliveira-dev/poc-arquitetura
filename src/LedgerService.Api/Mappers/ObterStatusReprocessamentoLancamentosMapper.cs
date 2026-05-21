using LedgerService.Api.Contracts.Requests;
using LedgerService.Api.Contracts.Responses;
using LedgerService.Application.Lancamentos.Queries;

namespace LedgerService.Api.Mappers;

public static class ObterStatusReprocessamentoLancamentosMapper
{
    public static ObterStatusReprocessamentoLancamentosResponse ToResponse(
        ObterStatusReprocessamentoLancamentosResult result)
        => new(
            result.ReprocessamentoId,
            result.MerchantId,
            result.DataInicial,
            result.DataFinal,
            result.Status,
            result.Motivo,
            result.SolicitadoEm);
}
