namespace PaymentService.Infrastructure.Ledger;

public sealed class LedgerAuthenticationException(string message) : Exception(message);
