namespace LedgerService.Application.Lancamentos.Events;

public sealed record LancamentoEstornoSolicitadoV1(
    Guid EstornoId,
    Guid LancamentoOriginalId,
    string MerchantId,
    string Motivo,
    string Status,
    string RequestedAt,
    string CorrelationId)
{
    public const string EventType = "LancamentoEstornoSolicitado.v1";
}
