extern alias LedgerWorker;

using LedgerService.Application.Outbox.Retry;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Observability;
using LedgerService.Infrastructure.Persistence;
using LedgerService.Infrastructure.Persistence.Repositories;
using LedgerService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using IOutboxMessagePublisher = LedgerWorker::LedgerService.Worker.Messaging.Abstractions.IOutboxMessagePublisher;
using OutboxKafkaPublisherService = LedgerWorker::LedgerService.Worker.Outbox.OutboxKafkaPublisherService;
using OutboxPublisherOptions = LedgerWorker::LedgerService.Worker.Outbox.OutboxPublisherOptions;

namespace LedgerService.IntegrationTests.Outbox;

[Collection(PostgresLedgerCollection.Name)]
public sealed class OutboxPublisherWorkerTests
{
    private readonly PostgresLedgerFixture _fixture;

    public OutboxPublisherWorkerTests(PostgresLedgerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Worker_should_increment_retry_and_schedule_next_retry_when_publish_fails()
    {
        await _fixture.CleanAsync();
        var outboxId = await SeedPendingMessageAsync();

        await using var provider = CreateProvider(maxAttempts: 5);
        var sut = provider.GetRequiredService<OutboxKafkaPublisherService>();

        await sut.ProcessOnceAsync(TestContext.Current.CancellationToken);

        await using var db = CreateDbContext();
        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == outboxId, TestContext.Current.CancellationToken);
        Assert.Equal(OutboxStatus.Pending, refreshed.Status);
        Assert.Equal(1, refreshed.RetryCount);
        Assert.NotNull(refreshed.NextRetryAt);
        Assert.Contains("Kafka unavailable", refreshed.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Worker_should_move_message_to_dead_letter_after_max_retries()
    {
        await _fixture.CleanAsync();
        var outboxId = await SeedPendingMessageAsync();

        await using var provider = CreateProvider(maxAttempts: 1);
        var sut = provider.GetRequiredService<OutboxKafkaPublisherService>();

        await sut.ProcessOnceAsync(TestContext.Current.CancellationToken);

        await using var db = CreateDbContext();
        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == outboxId, TestContext.Current.CancellationToken);
        Assert.Equal(OutboxStatus.DeadLetter, refreshed.Status);
        Assert.Equal(1, refreshed.RetryCount);
        Assert.Null(refreshed.NextRetryAt);
        Assert.Contains("Kafka unavailable", refreshed.LastError, StringComparison.Ordinal);
    }

    private async Task<Guid> SeedPendingMessageAsync()
    {
        await using var db = CreateDbContext();
        var message = new OutboxMessage(
            aggregateType: "LedgerEntry",
            aggregateId: Guid.NewGuid(),
            eventType: "LedgerEntryCreated.v1",
            payload: "{}",
            occurredAt: DateTime.Now.AddMinutes(-1),
            correlationId: Guid.NewGuid(),
            traceParent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return message.Id;
    }

    private ServiceProvider CreateProvider(int maxAttempts)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_fixture.ConnectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
        services.AddSingleton<OutboxMetrics>();
        services.AddSingleton<IOutboxMessagePublisher, FailingOutboxMessagePublisher>();
        services.AddSingleton<IJitterProvider>(new FixedJitterProvider(TimeSpan.Zero));
        services.AddSingleton<IRetryStrategy, ExponentialBackoffRetryStrategy>();
        services.AddSingleton(Options.Create(new OutboxPublisherOptions
        {
            BatchSize = 10,
            MaxAttempts = maxAttempts,
            MaxParallelism = 1,
            BaseBackoffSeconds = 1,
            LockDurationSeconds = 30,
            PollingIntervalSeconds = 1
        }));
        services.AddSingleton(NullLogger<OutboxKafkaPublisherService>.Instance);
        services.AddSingleton<OutboxKafkaPublisherService>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    private sealed class FailingOutboxMessagePublisher : IOutboxMessagePublisher
    {
        public string ResolveDestination(OutboxMessage message) => "ledger.ledgerentry.created";

        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
            => throw new TimeoutException("Kafka unavailable");
    }

    private sealed class FixedJitterProvider : IJitterProvider
    {
        private readonly TimeSpan _jitter;

        public FixedJitterProvider(TimeSpan jitter) => _jitter = jitter;

        public TimeSpan NextJitter() => _jitter;
    }
}
