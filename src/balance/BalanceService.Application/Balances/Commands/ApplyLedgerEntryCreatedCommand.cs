using BalanceService.Application.IntegrationEvents;

using MediatR;

namespace BalanceService.Application.Balances.Commands;

public sealed record ApplyLedgerEntryCreatedCommand(
    LedgerEntryCreatedIntegrationEvent Event,
    string EventType = "LedgerEntryCreated.v2") : IRequest<ApplyLedgerEntryCreatedResult>;
