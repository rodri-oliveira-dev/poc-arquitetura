using LedgerService.Api.Contracts;
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
        => new(
            lancamentoId,
            request.Motivo,
            idempotencyKey,
            correlationId,
            authorizedMerchantIds);

    public static SolicitarEstornoLancamentoResponse ToResponse(SolicitarEstornoLancamentoResult result)
        => new(
            result.EstornoId,
            result.LancamentoOriginalId,
            result.Status,
            result.StatusUrl);
}
