namespace LedgerService.Application.Lancamentos.Queries;

public sealed record ObterStatusEstornoLancamentoResult(
    Guid EstornoId,
    Guid LancamentoOriginalId,
    string Status,
    string Motivo,
    DateTime SolicitadoEm);
