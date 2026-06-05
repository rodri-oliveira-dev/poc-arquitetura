namespace LedgerService.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresLedgerCollection : ICollectionFixture<PostgresLedgerFixture>
{
    public const string Name = "PostgreSQL ledger schema integration tests";
}
