extern alias LedgerWorker;

using System.Collections.Concurrent;
using LedgerService.Application.Outbox.Retry;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Observability;
using LedgerService.Infrastructure.Persistence;
using LedgerService.Infrastructure.Persistence.Repositories;
using LedgerService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using IOutboxMessagePublisher = LedgerWorker::LedgerService.Worker.Messaging.Abstractions.IOutboxMessagePublisher;
using MessagePublishException = LedgerWorker::LedgerService.Worker.Messaging.Abstractions.MessagePublishException;
using OutboxPublisherService = LedgerWorker::LedgerService.Worker.Outbox.OutboxPublisherService;
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
    public async Task Worker_should_mark_batch_as_processed_when_publish_succeeds()
    {
        await _fixture.CleanAsync();
        var outboxIds = await SeedPendingMessagesAsync(3);
        var publisher = new RecordingOutboxMessagePublisher();

        await using var provider = CreateProvider(publisher);
        var sut = provider.GetRequiredService<OutboxPublisherService>();

        await sut.ProcessOnceAsync(TestContext.Current.CancellationToken);

        await using var db = CreateDbContext();
        var messages = await db.OutboxMessages
            .Where(x => outboxIds.Contains(x.Id))
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, messages.Count);
        Assert.All(messages, message => Assert.Equal(OutboxStatus.Processed, message.Status));
        Assert.Equal(outboxIds.Order(), publisher.PublishedIds.Order());
    }

    [Fact]
    public async Task Worker_should_increment_retry_and_schedule_next_retry_when_publish_fails()
    {
        await _fixture.CleanAsync();
        var outboxId = await SeedPendingMessageAsync();

        await using var provider = CreateProvider(new FailingOutboxMessagePublisher(), maxAttempts: 5);
        var sut = provider.GetRequiredService<OutboxPublisherService>();

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

        await using var provider = CreateProvider(new FailingOutboxMessagePublisher(), maxAttempts: 1);
        var sut = provider.GetRequiredService<OutboxPublisherService>();

        await sut.ProcessOnceAsync(TestContext.Current.CancellationToken);

        await using var db = CreateDbContext();
        var refreshed = await db.OutboxMessages.SingleAsync(x => x.Id == outboxId, TestContext.Current.CancellationToken);
        Assert.Equal(OutboxStatus.DeadLetter, refreshed.Status);
        Assert.Equal(1, refreshed.RetryCount);
        Assert.Null(refreshed.NextRetryAt);
        Assert.Contains("Kafka unavailable", refreshed.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClaimPendingAsync_concurrent_should_lock_message_for_only_one_publisher()
    {
        await _fixture.CleanAsync();
        var outboxId = await SeedPendingMessageAsync();
        var now = DateTime.UtcNow;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = ClaimAfterGateAsync("publisher-1", now, start.Task);
        var second = ClaimAfterGateAsync("publisher-2", now, start.Task);

        start.SetResult();
        var claimed = await Task.WhenAll(first, second);

        Assert.Equal(1, claimed.Sum(x => x.Count));
        Assert.Single(claimed.SelectMany(x => x), x => x.Id == outboxId);

        await using var db = CreateDbContext();
        var refreshed = await db.OutboxMessages.SingleAsync(
            x => x.Id == outboxId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OutboxStatus.Processing, refreshed.Status);
        Assert.Contains(refreshed.LockOwner, new[] { "publisher-1", "publisher-2" });
        Assert.NotNull(refreshed.LockedUntil);
    }

    [Fact]
    public async Task Worker_should_respect_max_parallelism()
    {
        await _fixture.CleanAsync();
        var outboxIds = await SeedPendingMessagesAsync(4);
        var publisher = new ConcurrencyTrackingOutboxMessagePublisher(expectedParallelism: 2);

        await using var provider = CreateProvider(publisher, maxParallelism: 2);
        var sut = provider.GetRequiredService<OutboxPublisherService>();

        var processing = sut.ProcessOnceAsync(TestContext.Current.CancellationToken);
        await publisher.ExpectedParallelismReached.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, publisher.PeakParallelism);

        publisher.Release();
        await processing;

        await using var db = CreateDbContext();
        var messages = await db.OutboxMessages
            .Where(x => outboxIds.Contains(x.Id))
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(messages, message => Assert.Equal(OutboxStatus.Processed, message.Status));
        Assert.Equal(2, publisher.PeakParallelism);
    }

    [Fact]
    public async Task Worker_should_skip_message_when_lock_expires_before_publication()
    {
        await _fixture.CleanAsync();
        var firstOutboxId = await SeedPendingMessageAsync(DateTime.UtcNow.AddMinutes(-2));
        var secondOutboxId = await SeedPendingMessageAsync(DateTime.UtcNow.AddMinutes(-1));
        var publisher = new BlockingOutboxMessagePublisher(firstOutboxId);

        await using var provider = CreateProvider(publisher, maxParallelism: 1);
        var sut = provider.GetRequiredService<OutboxPublisherService>();

        var processing = sut.ProcessOnceAsync(TestContext.Current.CancellationToken);
        await publisher.FirstPublishStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        await using (var db = CreateDbContext())
        {
            await db.OutboxMessages
                .Where(x => x.Id == secondOutboxId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.LockedUntil, DateTime.UtcNow.AddSeconds(-1)),
                    TestContext.Current.CancellationToken);
        }

        publisher.Release();
        await processing;

        await using var verificationDb = CreateDbContext();
        var first = await verificationDb.OutboxMessages.SingleAsync(
            x => x.Id == firstOutboxId,
            TestContext.Current.CancellationToken);
        var second = await verificationDb.OutboxMessages.SingleAsync(
            x => x.Id == secondOutboxId,
            TestContext.Current.CancellationToken);
        Assert.Equal(OutboxStatus.Processed, first.Status);
        Assert.Equal(OutboxStatus.Processing, second.Status);
        Assert.DoesNotContain(secondOutboxId, publisher.PublishedIds);
    }

    [Fact]
    public async Task Worker_should_isolate_unexpected_message_error_and_process_remaining_batch()
    {
        await _fixture.CleanAsync();
        var outboxIds = await SeedPendingMessagesAsync(3);
        var publisher = new UnexpectedFailureOutboxMessagePublisher(outboxIds[1]);
        var logger = new RecordingLogger<OutboxPublisherService>();

        await using var provider = CreateProvider(publisher, logger: logger);
        var sut = provider.GetRequiredService<OutboxPublisherService>();

        await sut.ProcessOnceAsync(TestContext.Current.CancellationToken);

        await using var db = CreateDbContext();
        var messages = await db.OutboxMessages
            .Where(x => outboxIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, TestContext.Current.CancellationToken);
        Assert.Equal(OutboxStatus.Processed, messages[outboxIds[0]].Status);
        Assert.Equal(OutboxStatus.Processing, messages[outboxIds[1]].Status);
        Assert.Equal(OutboxStatus.Processed, messages[outboxIds[2]].Status);
        Assert.DoesNotContain(outboxIds[1], publisher.PublishedIds);
        Assert.Contains(logger.Entries, entry =>
            entry.EventId == 1010 &&
            entry.Message.Contains(outboxIds[1].ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Worker_should_propagate_cancellation_during_message_publication()
    {
        await _fixture.CleanAsync();
        await SeedPendingMessageAsync();
        var publisher = new CancellationAwareOutboxMessagePublisher();

        await using var provider = CreateProvider(publisher);
        var sut = provider.GetRequiredService<OutboxPublisherService>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);

        var processing = sut.ProcessOnceAsync(cancellation.Token);
        await publisher.PublishStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processing);
    }

    private async Task<Guid> SeedPendingMessageAsync(DateTime? occurredAt = null)
    {
        await using var db = CreateDbContext();
        var message = new OutboxMessage(
            aggregateType: "LedgerEntry",
            aggregateId: Guid.NewGuid(),
            eventType: "LedgerEntryCreated.v1",
            payload: "{}",
            occurredAt: occurredAt ?? DateTime.UtcNow.AddMinutes(-1),
            correlationId: Guid.NewGuid(),
            traceParent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return message.Id;
    }

    private async Task<Guid[]> SeedPendingMessagesAsync(int count)
    {
        var ids = new List<Guid>(count);
        for (var index = 0; index < count; index++)
            ids.Add(await SeedPendingMessageAsync(DateTime.UtcNow.AddMinutes(-count + index)));

        return ids.ToArray();
    }

    private ServiceProvider CreateProvider(
        IOutboxMessagePublisher publisher,
        int maxAttempts = 5,
        int maxParallelism = 1,
        ILogger<OutboxPublisherService>? logger = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_fixture.ConnectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
        services.AddSingleton<OutboxMetrics>();
        services.AddSingleton(publisher);
        services.AddSingleton<IJitterProvider>(new FixedJitterProvider(TimeSpan.Zero));
        services.AddSingleton<IRetryStrategy, ExponentialBackoffRetryStrategy>();
        services.AddSingleton(Options.Create(new OutboxPublisherOptions
        {
            BatchSize = 10,
            MaxAttempts = maxAttempts,
            MaxParallelism = maxParallelism,
            BaseBackoffSeconds = 1,
            LockDurationSeconds = 30,
            PollingIntervalSeconds = 1
        }));
        services.AddSingleton(logger ?? NullLogger<OutboxPublisherService>.Instance);
        services.AddSingleton<OutboxPublisherService>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    private async Task<IReadOnlyList<OutboxMessage>> ClaimAfterGateAsync(
        string lockOwner,
        DateTime now,
        Task gate)
    {
        await gate;

        await using var db = CreateDbContext();
        var repository = new OutboxMessageRepository(db);
        return await repository.ClaimPendingAsync(
            batchSize: 10,
            now,
            lockOwner,
            lockDuration: TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken);
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
            => throw new MessagePublishException("Kafka unavailable", new TimeoutException("Kafka unavailable"));
    }

    private class RecordingOutboxMessagePublisher : IOutboxMessagePublisher
    {
        private readonly ConcurrentBag<Guid> _publishedIds = [];

        public IReadOnlyCollection<Guid> PublishedIds => _publishedIds;

        public string ResolveDestination(OutboxMessage message) => "ledger.ledgerentry.created";

        public virtual Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            _publishedIds.Add(message.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class ConcurrencyTrackingOutboxMessagePublisher : RecordingOutboxMessagePublisher
    {
        private readonly int _expectedParallelism;
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activePublishers;
        private int _peakParallelism;

        public ConcurrencyTrackingOutboxMessagePublisher(int expectedParallelism)
        {
            _expectedParallelism = expectedParallelism;
        }

        public TaskCompletionSource ExpectedParallelismReached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int PeakParallelism => _peakParallelism;

        public override async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            await base.PublishAsync(message, cancellationToken);

            var activePublishers = Interlocked.Increment(ref _activePublishers);
            UpdatePeakParallelism(activePublishers);
            if (activePublishers == _expectedParallelism)
                ExpectedParallelismReached.TrySetResult();

            try
            {
                await _release.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _activePublishers);
            }
        }

        public void Release() => _release.TrySetResult();

        private void UpdatePeakParallelism(int activePublishers)
        {
            var peak = _peakParallelism;
            while (activePublishers > peak)
            {
                var previous = Interlocked.CompareExchange(ref _peakParallelism, activePublishers, peak);
                if (previous == peak)
                    return;

                peak = previous;
            }
        }
    }

    private sealed class BlockingOutboxMessagePublisher : RecordingOutboxMessagePublisher
    {
        private readonly Guid _blockedOutboxId;
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingOutboxMessagePublisher(Guid blockedOutboxId)
        {
            _blockedOutboxId = blockedOutboxId;
        }

        public TaskCompletionSource FirstPublishStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            await base.PublishAsync(message, cancellationToken);
            if (message.Id != _blockedOutboxId)
                return;

            FirstPublishStarted.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
        }

        public void Release() => _release.TrySetResult();
    }

    private sealed class UnexpectedFailureOutboxMessagePublisher : RecordingOutboxMessagePublisher
    {
        private readonly Guid _failingOutboxId;

        public UnexpectedFailureOutboxMessagePublisher(Guid failingOutboxId)
        {
            _failingOutboxId = failingOutboxId;
        }

        public override Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            if (message.Id == _failingOutboxId)
                throw new InvalidOperationException("Unexpected publisher failure.");

            return base.PublishAsync(message, cancellationToken);
        }
    }

    private sealed class CancellationAwareOutboxMessagePublisher : RecordingOutboxMessagePublisher
    {
        public TaskCompletionSource PublishStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            PublishStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly ConcurrentBag<LogEntry> _entries = [];

        public IReadOnlyCollection<LogEntry> Entries => _entries;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _entries.Add(new LogEntry(eventId.Id, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(int EventId, string Message);

    private sealed class FixedJitterProvider : IJitterProvider
    {
        private readonly TimeSpan _jitter;

        public FixedJitterProvider(TimeSpan jitter) => _jitter = jitter;

        public TimeSpan NextJitter() => _jitter;
    }
}
