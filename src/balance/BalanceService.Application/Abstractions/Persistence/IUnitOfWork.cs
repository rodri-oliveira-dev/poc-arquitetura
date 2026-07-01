namespace BalanceService.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
