namespace BalanceService.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresBalanceCollection : ICollectionFixture<PostgresBalanceFixture>
{
    public const string Name = "PostgreSQL balance schema integration tests";
}
