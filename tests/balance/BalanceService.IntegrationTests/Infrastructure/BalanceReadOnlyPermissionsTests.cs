using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace BalanceService.IntegrationTests.Infrastructure;

[Trait("Category", "Container")]
[Trait("Category", "Integration")]
[Collection(PostgresBalanceCollection.Name)]
public sealed class BalanceReadOnlyPermissionsTests(PostgresBalanceFixture fixture)
{
    private readonly PostgresBalanceFixture _fixture = fixture;

    [Fact]
    public async Task Balance_read_user_should_not_execute_dml_on_balance_schema()
    {
        await _fixture.CleanAsync();

        await using var readOnlyDb = _fixture.CreateReadOnlyDbContext();
        _ = await readOnlyDb.ProcessedEvents.CountAsync(TestContext.Current.CancellationToken);

        await AssertInsufficientPrivilegeAsync(
            """
            INSERT INTO balance.processed_events (id, event_id, merchant_id, occurred_at, processed_at)
            VALUES ('11111111-1111-4111-8111-111111111111', 'evt-read-only', 'merchant-read-only', now(), now());
            """);

        await AssertInsufficientPrivilegeAsync(
            "UPDATE balance.processed_events SET merchant_id = merchant_id WHERE event_id = 'evt-read-only';");

        await AssertInsufficientPrivilegeAsync(
            "DELETE FROM balance.processed_events WHERE event_id = 'evt-read-only';");
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL statements are fixed test inputs used only to assert PostgreSQL permission errors.")]
    private async Task AssertInsufficientPrivilegeAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_fixture.ReadConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }
}
