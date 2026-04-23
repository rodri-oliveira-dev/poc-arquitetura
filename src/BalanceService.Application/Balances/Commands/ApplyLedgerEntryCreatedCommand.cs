using BalanceService.Domain.Balances;

using MediatR;

namespace BalanceService.Application.Balances.Commands;

public sealed record ApplyLedgerEntryCreatedCommand(LedgerEntryCreatedEvent Event) : IRequest;
