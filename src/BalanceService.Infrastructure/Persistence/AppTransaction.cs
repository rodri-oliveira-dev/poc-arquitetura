using BalanceService.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace BalanceService.Infrastructure.Persistence;

public sealed class AppTransaction : IAppTransaction
{
    private readonly IDbContextTransaction _transaction;

    public AppTransaction(IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _transaction.CommitAsync(cancellationToken);

    public ValueTask DisposeAsync()
        => _transaction.DisposeAsync();
}
