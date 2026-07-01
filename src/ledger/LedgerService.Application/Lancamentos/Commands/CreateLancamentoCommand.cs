using LedgerService.Application.Common.Models;
using LedgerService.Application.Lancamentos.Inputs.CreateLancamento;

using MediatR;

namespace LedgerService.Application.Lancamentos.Commands;

public sealed record CreateLancamentoCommand(
    CreateLancamentoInput Input) : IRequest<LancamentoDto>;
