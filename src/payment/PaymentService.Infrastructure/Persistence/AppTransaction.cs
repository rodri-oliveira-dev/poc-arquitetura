using Microsoft.EntityFrameworkCore.Storage;

using PaymentService.Application.Abstractions.Persistence;

namespace PaymentService.Infrastructure.Persistence;

public sealed class AppTransaction(IDbContextTransaction transaction) : IAppTransaction
{
    private readonly IDbContextTransaction _transaction = transaction;

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _transaction.CommitAsync(cancellationToken);

    public ValueTask DisposeAsync()
        => _transaction.DisposeAsync();
}
