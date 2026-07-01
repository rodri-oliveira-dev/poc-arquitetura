namespace TransferService.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresTransferCollection : ICollectionFixture<PostgresTransferFixture>
{
    public const string Name = "PostgreSQL transfer schema integration tests";
}
