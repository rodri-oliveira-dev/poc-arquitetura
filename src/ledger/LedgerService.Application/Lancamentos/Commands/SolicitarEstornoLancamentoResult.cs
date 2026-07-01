namespace LedgerService.Application.Lancamentos.Commands;

public sealed record SolicitarEstornoLancamentoResult(
    Guid EstornoId,
    Guid LancamentoOriginalId,
    string Status,
    string StatusUrl,
    string MerchantId);
