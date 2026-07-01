using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Testcontainers.PostgreSql;

using TransferService.Application.Abstractions.Messaging;
using TransferService.Application.Abstractions.Persistence;
using TransferService.Application.Abstractions.Time;
using TransferService.Application.Transferencias.Commands;
using TransferService.Infrastructure.Messaging.Kafka;
using TransferService.Infrastructure.Persistence;
using TransferService.Infrastructure.Persistence.Outbox;
using TransferService.Infrastructure.Persistence.Repositories;

namespace TransferService.IntegrationTests.Infrastructure;

public sealed class PostgresTransferFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("docker.io/postgres:16")
        .WithDatabase("appdb")
        .WithUsername("appuser")
        .WithPassword("app123")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task CleanAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE transfer.outbox_messages, transfer.idempotency_records, transfer.transferencias_sagas;");
    }

    public TransferServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TransferServiceDbContext>()
            .UseNpgsql(
                ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "transfer"))
            .Options;

        return new TransferServiceDbContext(options);
    }

    public ServiceProvider CreateServiceProvider(DateTimeOffset now)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IClock>(new FixedClock(now));
        services.AddDbContext<TransferServiceDbContext>(options =>
            options.UseNpgsql(
                ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "transfer")));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TransferServiceDbContext>());
        services.AddScoped<ITransferenciaSagaRepository, TransferenciaSagaRepository>();
        services.AddScoped<ITransferenciaIdempotencyService, TransferenciaIdempotencyService>();
        services.AddScoped<ITransferenciaOutboxWriter, TransferenciaOutboxWriter>();
        services.AddSingleton(new TransferenciaSagaKafkaMetadataMapper(
            Options.Create(new TransferenciaKafkaTopicOptions
            {
                Solicitada = "transfer.transferencia.solicitada",
                DebitoCriado = "transfer.transferencia.debito-criado",
                CreditoCriado = "transfer.transferencia.credito-criado",
                Concluida = "transfer.transferencia.concluida",
                CompensacaoSolicitada = "transfer.transferencia.compensacao-solicitada",
                Compensada = "transfer.transferencia.compensada",
                Falhou = "transfer.transferencia.falhou"
            })));
        services.AddScoped<SolicitarTransferenciaCommandHandler>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow
        {
            get;
        }
    }
}
