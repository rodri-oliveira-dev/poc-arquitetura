using BalanceService.Domain.Balances;

namespace BalanceService.Application.Balances.Commands;

public sealed record ApplyLedgerEntryCreatedCommand(LedgerEntryCreatedEvent Event);
