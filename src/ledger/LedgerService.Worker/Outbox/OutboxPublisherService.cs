using System.Diagnostics;

using LedgerService.Application.Abstractions.Messaging;
using LedgerService.Application.Abstractions.Time;
using LedgerService.Application.Outbox.Retry;
using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Observability;
using LedgerService.Infrastructure.Persistence;
using LedgerService.Worker.Messaging.Abstractions;
using LedgerService.Worker.Observability;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LedgerService.Worker.Outbox;

public sealed class OutboxPublisherService(
    IServiceProvider serviceProvider,
    IOptions<OutboxPublisherOptions> options,
    IRetryStrategy retryStrategy,
    ILogger<OutboxPublisherService> logger,
    IClock? clock = null) : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("LedgerService.OutboxPublisher");
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IOptions<OutboxPublisherOptions> _options = options;
    private readonly IRetryStrategy _retryStrategy = retryStrategy;
    private readonly ILogger<OutboxPublisherService> _logger = logger;
    private readonly IClock _clock = clock ?? new SystemClock();
    private readonly string _lockOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.Value.PollingIntervalSeconds));

        _logger.PublisherStarted(_lockOwner, interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shutdown
            }
            catch (DbUpdateException ex)
            {
                _logger.PersistentPublisherError(ex);
            }
            catch (TimeoutException ex)
            {
                _logger.PublisherTimeout(ex);
            }
            catch (Exception ex)
            {
                _logger.UnhandledPublisherError(ex);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.PublisherStopped(_lockOwner);
    }

    internal async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var options = _options.Value;
        var now = _clock.UtcNow.UtcDateTime;
        var lockDuration = TimeSpan.FromSeconds(Math.Max(5, options.LockDurationSeconds));

        var claimed = await outboxRepo.ClaimPendingAsync(
            options.BatchSize,
            now,
            _lockOwner,
            lockDuration,
            cancellationToken);

        if (claimed.Count == 0)
            return;

        await uow.SaveChangesAsync(cancellationToken);

        _logger.OutboxMessagesClaimed(claimed.Count, _lockOwner, options.MaxParallelism);

        await Parallel.ForEachAsync(
            claimed,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, options.MaxParallelism),
                CancellationToken = cancellationToken
            },
            async (message, ct) => await PublishOneSafelyAsync(message.Id, ct));
    }

    private async Task PublishOneSafelyAsync(Guid outboxId, CancellationToken ct)
    {
        try
        {
            await PublishOneAsync(outboxId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.UnhandledOutboxMessageError(ex, outboxId);
        }
    }

    private async Task PublishOneAsync(Guid outboxId, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var publisher = scope.ServiceProvider.GetRequiredService<IOutboxMessagePublisher>();
        var metrics = scope.ServiceProvider.GetRequiredService<OutboxMetrics>();

        var options = _options.Value;

        var message = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == outboxId, ct);
        if (message is null)
            return;

        var now = _clock.UtcNow.UtcDateTime;
        if (!string.Equals(message.LockOwner, _lockOwner, StringComparison.Ordinal) ||
            (message.LockedUntil is not null && message.LockedUntil <= now))
        {
            _logger.OutboxMessageSkippedBecauseLockExpired(message.LockOwner, _lockOwner, message.LockedUntil);
            return;
        }

        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = message.CorrelationId,
            ["TraceId"] = Activity.Current?.TraceId.ToString(),
            ["OutboxId"] = message.Id,
            ["EventType"] = message.EventType,
            ["AggregateId"] = message.AggregateId
        });

        var topic = publisher.ResolveDestination(message);

        using var activity = StartProducerActivity(
            ActivitySource,
            "outbox.publish",
            message.TraceParent,
            message.TraceState,
            message.Baggage);

        activity?.SetTag("messaging.destination", topic);
        activity?.SetTag("messaging.operation", "publish");
        activity?.SetTag("ledger.outbox.id", message.Id.ToString());
        activity?.SetTag("ledger.outbox.event_type", message.EventType);
        activity?.SetTag("ledger.outbox.aggregate_id", message.AggregateId.ToString());
        if (message.CorrelationId is not null)
            activity?.SetTag("correlation_id", message.CorrelationId.ToString());

        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await publisher.PublishAsync(message, ct);

            await repo.MarkProcessedAsync(message.Id, _clock.UtcNow.UtcDateTime, ct);
            await uow.SaveChangesAsync(ct);

            metrics.RecordPublishAttempt(message.EventType, "success");
            metrics.RecordOutboxMessagePublished(message.EventType, topic, "success");
            metrics.RecordOutboxPublishDuration(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, message.EventType, topic, "success");
            _logger.OutboxMessageMarkedAsProcessed();
        }
        catch (MessagePublishException ex)
        {
            await HandlePublishFailureAsync(repo, uow, metrics, message, topic, startedAt, options, ex, ct);
        }
    }

    private async Task HandlePublishFailureAsync(
        IOutboxMessageRepository repo,
        IUnitOfWork uow,
        OutboxMetrics metrics,
        OutboxMessage message,
        string topic,
        long startedAt,
        OutboxPublisherOptions options,
        Exception exception,
        CancellationToken cancellationToken)
    {
        RecordOutboxPublishFailure(metrics, topic, message.EventType, startedAt);

        var retryCountAfterFailure = message.RetryCount + 1;
        var nextRetryAt = _retryStrategy.CalculateNextRetry(
            _clock.UtcNow.UtcDateTime,
            retryCountAfterFailure,
            TimeSpan.FromSeconds(options.BaseBackoffSeconds));

        var status = await repo.MarkFailedPublishAttemptAsync(
            message.Id,
            options.MaxAttempts,
            nextRetryAt,
            exception.ToString(),
            cancellationToken);

        await uow.SaveChangesAsync(cancellationToken);

        if (status == OutboxStatus.DeadLetter)
        {
            _logger.OutboxMessageMovedToDeadLetter(exception, message.Id);
            return;
        }

        _logger.OutboxPublishFailed(exception, nextRetryAt);
    }

    private static void RecordOutboxPublishFailure(
        OutboxMetrics metrics,
        string topic,
        string eventType,
        long startedAt)
    {
        metrics.RecordPublishAttempt(eventType, "failure");
        metrics.RecordOutboxMessagePublished(eventType, topic, "failure");
        metrics.RecordOutboxPublishDuration(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, eventType, topic, "failure");
    }

    private static Activity? StartProducerActivity(
        ActivitySource activitySource,
        string operationName,
        string? traceParent,
        string? traceState,
        string? baggage)
    {
        var activity = !string.IsNullOrWhiteSpace(traceParent) &&
            ActivityContext.TryParse(traceParent, traceState, out var parentContext)
                ? activitySource.StartActivity(operationName, ActivityKind.Producer, parentContext)
                : activitySource.StartActivity(operationName, ActivityKind.Producer);

        AddBaggage(activity, baggage);
        return activity;
    }

    private static void AddBaggage(Activity? activity, string? baggage)
    {
        if (activity is null || string.IsNullOrWhiteSpace(baggage))
            return;

        foreach (var item in baggage.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = item.Split(';', 2, StringSplitOptions.TrimEntries)[0];
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == pair.Length - 1)
                continue;

            activity.AddBaggage(pair[..separatorIndex], pair[(separatorIndex + 1)..]);
        }
    }
}
