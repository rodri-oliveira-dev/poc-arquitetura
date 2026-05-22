namespace LedgerService.Domain.Entities;

public enum OutboxStatus
{
    Pending = 1,
    Processing = 2,
    Processed = 3,
    DeadLetter = 4
}
