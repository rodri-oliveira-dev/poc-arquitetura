namespace TransferService.Domain.Sagas;

public enum TransferenciaSagaStep
{
    Created = 0,
    Processing = 1,
    DebitCreation = 2,
    DebitCreated = 3,
    CreditCreation = 4,
    Completed = 5,
    Compensation = 6,
    Compensated = 7,
    Failed = 8,
    Rejected = 9
}
