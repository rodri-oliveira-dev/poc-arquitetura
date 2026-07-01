namespace LedgerService.Domain.Entities;

public enum ReprocessamentoLancamentosStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    CompletedWithWarnings = 3,
    Failed = 4,
    Rejected = 5,
    Canceled = 6
}
