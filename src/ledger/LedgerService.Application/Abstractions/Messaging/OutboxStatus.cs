namespace LedgerService.Application.Abstractions.Messaging;

public enum OutboxStatus
{
    Pending = 1,
    Processing = 2,
    Processed = 3,
    DeadLetter = 4
}
