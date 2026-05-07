using Testcontainers.PostgreSql;

namespace LedgerService.IntegrationTests.Infrastructure;

public sealed class PostgresLedgerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("docker.io/postgres:16")
        .WithDatabase("appdb")
        .WithUsername("appuser")
        .WithPassword("app123")
        .WithPortBinding(15432, 5432)
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var factory = new PostgresLedgerApiFactory(ConnectionString);
        await factory.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
