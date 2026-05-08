namespace LedgerService.Application.Lancamentos.Queries;

public sealed record ObterStatusReprocessamentoLancamentosResult(
    Guid ReprocessamentoId,
    string MerchantId,
    DateOnly DataInicial,
    DateOnly DataFinal,
    string Status,
    string Motivo,
    DateTime SolicitadoEm);
