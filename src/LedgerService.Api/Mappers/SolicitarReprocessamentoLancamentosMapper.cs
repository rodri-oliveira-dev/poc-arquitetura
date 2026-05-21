using LedgerService.Api.Contracts.Requests;
using LedgerService.Api.Contracts.Responses;
using LedgerService.Application.Lancamentos.Commands;

namespace LedgerService.Api.Mappers;

public static class SolicitarReprocessamentoLancamentosMapper
{
    public static SolicitarReprocessamentoLancamentosCommand ToCommand(
        SolicitarReprocessamentoLancamentosRequest request,
        string idempotencyKey,
        string correlationId,
        IReadOnlyCollection<string> authorizedMerchantIds)
        => new(
            request.MerchantId,
            request.DataInicial,
            request.DataFinal,
            request.Motivo,
            idempotencyKey,
            correlationId,
            authorizedMerchantIds);

    public static SolicitarReprocessamentoLancamentosResponse ToResponse(
        SolicitarReprocessamentoLancamentosResult result)
        => new(
            result.ReprocessamentoId,
            result.MerchantId,
            result.DataInicial,
            result.DataFinal,
            result.Status,
            result.StatusUrl);
}
