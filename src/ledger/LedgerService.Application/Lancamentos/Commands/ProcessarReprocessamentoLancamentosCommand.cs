using MediatR;

namespace LedgerService.Application.Lancamentos.Commands;

public sealed record ProcessarReprocessamentoLancamentosCommand(Guid ReprocessamentoId) : IRequest;
