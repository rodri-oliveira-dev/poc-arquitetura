namespace TransferService.Worker.Ledger;

public interface ILedgerAccessTokenProvider
{
    ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
