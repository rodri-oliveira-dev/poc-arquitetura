using MediatR;

namespace LedgerService.Application.Lancamentos.Commands;

public sealed record ProcessarEstornoLancamentoCommand(Guid EstornoId) : IRequest;
