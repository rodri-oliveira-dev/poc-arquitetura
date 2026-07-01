using LedgerService.Api.Contracts.Requests;
using LedgerService.Api.Contracts.Responses;
using LedgerService.Application.Lancamentos.Commands;

namespace LedgerService.Api.Mappers;

public static class SolicitarEstornoLancamentoMapper
{
    public static SolicitarEstornoLancamentoCommand ToCommand(
        Guid lancamentoId,
        SolicitarEstornoLancamentoRequest request,
        string idempotencyKey,
        string correlationId,
        IReadOnlyCollection<string> authorizedMerchantIds)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new(
            lancamentoId,
            request.Motivo,
            idempotencyKey,
            correlationId,
            authorizedMerchantIds);
    }

    public static SolicitarEstornoLancamentoResponse ToResponse(SolicitarEstornoLancamentoResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new(
            result.EstornoId,
            result.LancamentoOriginalId,
            result.Status,
            result.StatusUrl);
    }
}
