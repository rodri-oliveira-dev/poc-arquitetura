using LedgerService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerService.IntegrationTests.Infrastructure;

[Collection(PostgresLedgerCollection.Name)]
public sealed class LedgerTimestampPersistenceTests : IAsyncLifetime
{
    private static readonly HashSet<(string Table, string Column)> ExpectedTimestampColumns =
    [
        ("ledger_entries", "occurred_at"),
        ("ledger_entries", "created_at"),
        ("idempotency_records", "created_at"),
        ("idempotency_records", "expires_at"),
        ("outbox_messages", "occurred_at"),
        ("outbox_messages", "next_retry_at"),
        ("outbox_messages", "processed_at"),
        ("outbox_messages", "locked_until"),
        ("outbox_messages", "last_requeued_at"),
        ("estornos_lancamentos", "created_at"),
        ("estornos_lancamentos", "processing_started_at"),
        ("estornos_lancamentos", "completed_at"),
        ("estornos_lancamentos", "rejected_at"),
        ("estornos_lancamentos", "failed_at"),
        ("reprocessamentos_lancamentos", "created_at"),
        ("reprocessamentos_lancamentos", "processing_started_at"),
        ("reprocessamentos_lancamentos", "completed_at"),
        ("reprocessamentos_lancamentos", "failed_at"),
        ("reprocessamentos_lancamentos", "rejected_at")
    ];

    private readonly PostgresLedgerApiFactory _factory;

    public LedgerTimestampPersistenceTests(PostgresLedgerFixture fixture)
    {
        _factory = new PostgresLedgerApiFactory(fixture.ConnectionString);
    }

    public async ValueTask InitializeAsync()
    {
        await _factory.CleanAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Baseline_schema_should_persist_utc_timestamps_as_timestamptz()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entryId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var expectedUtc = new DateTime(2026, 6, 1, 12, 30, 0, DateTimeKind.Utc);

        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO ledger.ledger_entries
                (id, merchant_id, type, amount, occurred_at, description, external_reference, correlation_id, created_at)
            VALUES
                ({0}, 'm1', 'Credit', 10.00, TIMESTAMPTZ '2026-06-01 12:30:00+00', 'Venda', {1}, {2}, TIMESTAMPTZ '2026-06-01 12:30:00+00');
            """,
            [
                entryId,
                $"ext-{Guid.NewGuid():N}",
                correlationId
            ],
            TestContext.Current.CancellationToken);

        var actualColumns = await GetTimestampWithTimeZoneColumnsAsync(db);
        Assert.True(ExpectedTimestampColumns.SetEquals(actualColumns));

        var saved = await db.LedgerEntries
            .AsNoTracking()
            .SingleAsync(x => x.Id == entryId, TestContext.Current.CancellationToken);
        Assert.Equal(expectedUtc, saved.OccurredAt);
        Assert.Equal(DateTimeKind.Utc, saved.OccurredAt.Kind);
        Assert.Equal(expectedUtc, saved.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);
    }

    private static async Task<HashSet<(string Table, string Column)>> GetTimestampWithTimeZoneColumnsAsync(
        AppDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT table_name, column_name
            FROM information_schema.columns
            WHERE table_schema = 'ledger'
              AND data_type = 'timestamp with time zone';
            """;

        var columns = new HashSet<(string Table, string Column)>();
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            columns.Add((reader.GetString(0), reader.GetString(1)));
        }

        return columns;
    }
}
