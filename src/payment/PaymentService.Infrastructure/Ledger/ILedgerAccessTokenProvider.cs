namespace PaymentService.Infrastructure.Ledger;

public interface ILedgerAccessTokenProvider
{
    ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
