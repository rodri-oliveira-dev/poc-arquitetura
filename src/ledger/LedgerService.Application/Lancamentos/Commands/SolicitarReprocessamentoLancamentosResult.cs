namespace LedgerService.Application.Lancamentos.Commands;

public sealed record SolicitarReprocessamentoLancamentosResult(
    Guid ReprocessamentoId,
    string MerchantId,
    DateOnly DataInicial,
    DateOnly DataFinal,
    string Status,
    string StatusUrl);
