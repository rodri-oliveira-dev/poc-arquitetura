namespace BalanceService.Application.Balances.Commands;

public sealed record ApplyLedgerEntryCreatedResult(bool Applied, bool Duplicate)
{
    public static ApplyLedgerEntryCreatedResult Processed { get; } = new(Applied: true, Duplicate: false);

    public static ApplyLedgerEntryCreatedResult IgnoredDuplicate { get; } = new(Applied: false, Duplicate: true);
}
