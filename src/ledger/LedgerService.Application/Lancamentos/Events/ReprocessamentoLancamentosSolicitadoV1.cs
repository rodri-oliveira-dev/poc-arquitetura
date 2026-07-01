namespace LedgerService.Application.Lancamentos.Events;

public sealed record ReprocessamentoLancamentosSolicitadoV1(
    Guid ReprocessamentoId,
    string MerchantId,
    DateOnly DataInicial,
    DateOnly DataFinal,
    string Motivo,
    string Status,
    string RequestedAt,
    string CorrelationId)
{
    public const string EventType = "ReprocessamentoLancamentosSolicitado.v1";
}
