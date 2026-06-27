namespace IdentityService.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresIdentityCollection : ICollectionFixture<PostgresIdentityFixture>
{
    public const string Name = "PostgreSQL identity schema integration tests";
}
