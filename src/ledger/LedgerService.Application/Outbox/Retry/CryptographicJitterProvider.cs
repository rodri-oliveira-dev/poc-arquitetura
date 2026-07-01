using System.Security.Cryptography;

namespace LedgerService.Application.Outbox.Retry;

public sealed class CryptographicJitterProvider : IJitterProvider
{
    private const int MaxJitterMilliseconds = 250;

    public TimeSpan NextJitter()
        => TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32(0, MaxJitterMilliseconds));
}
