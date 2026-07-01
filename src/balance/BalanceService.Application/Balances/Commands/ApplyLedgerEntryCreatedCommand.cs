using BalanceService.Domain.Balances;

using MediatR;

namespace BalanceService.Application.Balances.Commands;

public sealed record ApplyLedgerEntryCreatedCommand(
    LedgerEntryCreatedEvent Event,
    string EventType = "LedgerEntryCreated.v2") : IRequest<ApplyLedgerEntryCreatedResult>;
