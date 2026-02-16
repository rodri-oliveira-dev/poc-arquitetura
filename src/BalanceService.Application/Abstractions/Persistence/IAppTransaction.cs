namespace BalanceService.Application.Abstractions.Persistence;

public interface IAppTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
