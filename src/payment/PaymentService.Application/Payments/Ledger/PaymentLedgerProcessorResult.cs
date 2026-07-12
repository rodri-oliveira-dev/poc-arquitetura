namespace PaymentService.Application.Payments.Ledger;

public sealed record PaymentLedgerProcessorResult(int Claimed, int Completed, int RetryScheduled, int FailedDefinitive, int DeadLettered);
