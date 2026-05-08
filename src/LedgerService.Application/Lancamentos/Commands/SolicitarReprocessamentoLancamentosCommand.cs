using MediatR;

namespace LedgerService.Application.Lancamentos.Commands;

public sealed record SolicitarReprocessamentoLancamentosCommand(
    string MerchantId,
    DateOnly DataInicial,
    DateOnly DataFinal,
    string Motivo,
    string IdempotencyKey,
    string CorrelationId,
    IReadOnlyCollection<string> AuthorizedMerchantIds) : IRequest<SolicitarReprocessamentoLancamentosResult>;
