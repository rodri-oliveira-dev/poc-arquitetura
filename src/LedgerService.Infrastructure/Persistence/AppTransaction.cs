using Microsoft.EntityFrameworkCore.Storage;
using LedgerService.Domain.Repositories;

namespace LedgerService.Infrastructure.Persistence;

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