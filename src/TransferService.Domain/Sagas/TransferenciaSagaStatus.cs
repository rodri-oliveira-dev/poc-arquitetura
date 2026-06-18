namespace TransferService.Domain.Sagas;

public enum TransferenciaSagaStatus
{
    Pending = 0,
    Processing = 1,
    DebitCreating = 2,
    DebitCreated = 3,
    CreditCreating = 4,
    Completed = 5,
    CompensationRequested = 6,
    Compensated = 7,
    Failed = 8,
    Rejected = 9
}
