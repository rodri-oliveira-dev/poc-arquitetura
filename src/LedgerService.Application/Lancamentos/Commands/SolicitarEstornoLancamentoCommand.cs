using MediatR;

namespace LedgerService.Application.Lancamentos.Commands;

public sealed record SolicitarEstornoLancamentoCommand(
    Guid LancamentoId,
    string Motivo,
    string IdempotencyKey,
    string CorrelationId,
    IReadOnlyCollection<string> AuthorizedMerchantIds) : IRequest<SolicitarEstornoLancamentoResult>;
