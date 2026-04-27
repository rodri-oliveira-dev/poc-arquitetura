using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Messaging.Kafka;
using LedgerService.Infrastructure.Persistence;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Cryptography;

namespace LedgerService.Infrastructure.Outbox;

public sealed partial class OutboxKafkaPublisherService : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("LedgerService.OutboxPublisher");
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<OutboxPublisherOptions> _options;
    private readonly ILogger<OutboxKafkaPublisherService> _logger;
    private readonly string _lockOwner;

    public OutboxKafkaPublisherService(
        IServiceProvider serviceProvider,
        IOptions<OutboxPublisherOptions> options,
        ILogger<OutboxKafkaPublisherService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
        _lockOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.Value.PollingIntervalSeconds));

        LogOutboxPublisherStarted(_logger, _lockOwner, interval.TotalSeconds);

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
                LogPersistentOutboxPublisherError(_logger, ex);
            }
            catch (TimeoutException ex)
            {
                LogOutboxPublisherTimeout(_logger, ex);
            }
            catch (KafkaException ex)
            {
                LogUnhandledOutboxPublisherError(_logger, ex);
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

        LogOutboxPublisherStopped(_logger, _lockOwner);
    }

    private async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var options = _options.Value;
        var now = DateTime.Now;
        var lockDuration = TimeSpan.FromSeconds(Math.Max(5, options.LockDurationSeconds));

        var claimed = await outboxRepo.ClaimPendingAsync(
            options.BatchSize,
            now,
            _lockOwner,
            lockDuration,
            cancellationToken);

        if (claimed.Count == 0)
            return;

        // Confirma o claim
        await uow.SaveChangesAsync(cancellationToken);

        LogOutboxMessagesClaimed(_logger, claimed.Count, _lockOwner, options.MaxParallelism);

        using var throttler = new SemaphoreSlim(Math.Max(1, options.MaxParallelism));
        var tasks = new List<Task>(claimed.Count);

        foreach (var msg in claimed)
        {
            await throttler.WaitAsync(cancellationToken);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await PublishOneAsync(msg.Id, cancellationToken);
                }
                finally
                {
                    throttler.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    private async Task PublishOneAsync(Guid outboxId, CancellationToken ct)
    {
        // Escopo isolado por mensagem para evitar concorrência no DbContext.
        using var scope = _serviceProvider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var producer = scope.ServiceProvider.GetRequiredService<IOutboxEventProducer>();

        var options = _options.Value;

        var message = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == outboxId, ct);
        if (message is null)
            return;

        // Segurança extra: se o lock expirou e/ou foi "roubado" por outra instância,
        // não publicamos para reduzir duplicidade por concorrência.
        var now = DateTime.Now;
        if (!string.Equals(message.LockOwner, _lockOwner, StringComparison.Ordinal) ||
            (message.LockedUntil is not null && message.LockedUntil <= now))
        {
            LogOutboxMessageSkippedBecauseLockExpired(_logger, message.LockOwner, _lockOwner, message.LockedUntil);
            return;
        }

        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = message.CorrelationId,
            ["OutboxId"] = message.Id,
            ["EventType"] = message.EventType,
            ["AggregateId"] = message.AggregateId
        });

        using var activity = ActivitySource.StartActivity(
            "outbox.publish",
            ActivityKind.Producer);

        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination", "kafka");
        activity?.SetTag("messaging.operation", "publish");
        activity?.SetTag("ledger.outbox.id", message.Id.ToString());
        activity?.SetTag("ledger.outbox.event_type", message.EventType);
        activity?.SetTag("ledger.outbox.aggregate_id", message.AggregateId.ToString());
        if (message.CorrelationId is not null)
            activity?.SetTag("correlation_id", message.CorrelationId.ToString());

        try
        {
            await producer.ProduceAsync(message, ct);

            // importante: só marca SENT após confirmação do ProduceAsync
            await repo.MarkSentAsync(message.Id, DateTime.Now, ct);
            await uow.SaveChangesAsync(ct);

            LogOutboxMessageMarkedAsSent(_logger);
        }
        catch (ProduceException<string, string> ex)
        {
            var nextAttemptAt = ComputeNextAttempt(DateTime.Now, message.Attempts + 1, options.BaseBackoffSeconds);
            await repo.MarkFailedAttemptAsync(message.Id, options.MaxAttempts, nextAttemptAt, ex.Message, ct);
            await uow.SaveChangesAsync(ct);

            LogOutboxPublishFailed(_logger, ex, nextAttemptAt);
        }
        catch (KafkaException ex)
        {
            var nextAttemptAt = ComputeNextAttempt(DateTime.Now, message.Attempts + 1, options.BaseBackoffSeconds);
            await repo.MarkFailedAttemptAsync(message.Id, options.MaxAttempts, nextAttemptAt, ex.Message, ct);
            await uow.SaveChangesAsync(ct);

            LogOutboxPublishFailed(_logger, ex, nextAttemptAt);
        }
        catch (TimeoutException ex)
        {
            var nextAttemptAt = ComputeNextAttempt(DateTime.Now, message.Attempts + 1, options.BaseBackoffSeconds);
            await repo.MarkFailedAttemptAsync(message.Id, options.MaxAttempts, nextAttemptAt, ex.Message, ct);
            await uow.SaveChangesAsync(ct);

            LogOutboxPublishFailed(_logger, ex, nextAttemptAt);
        }
    }

    private static DateTime ComputeNextAttempt(DateTime now, int attemptNumber, int baseBackoffSeconds)
    {
        var baseDelay = TimeSpan.FromSeconds(Math.Max(1, baseBackoffSeconds));
        var exp = Math.Pow(2, Math.Min(10, Math.Max(0, attemptNumber - 1)));
        var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * exp);
        // CA5394: Random não é considerado seguro. Aqui o valor é apenas "jitter" de retry,
        // mas usamos RNG criptograficamente seguro para manter o build limpo com analyzers.
        var jitterMs = RandomNumberGenerator.GetInt32(0, 250);
        return now.Add(delay).AddMilliseconds(jitterMs);
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "OutboxKafkaPublisherService started (owner={LockOwner}, interval={IntervalSeconds}s)")]
    private static partial void LogOutboxPublisherStarted(ILogger logger, string lockOwner, double intervalSeconds);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "Erro persistente no OutboxKafkaPublisherService. Vai retentar no proximo ciclo.")]
    private static partial void LogPersistentOutboxPublisherError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "Timeout no OutboxKafkaPublisherService. Vai retentar no proximo ciclo.")]
    private static partial void LogOutboxPublisherTimeout(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Error, Message = "Erro não tratado no OutboxKafkaPublisherService. Vai retentar no próximo ciclo.")]
    private static partial void LogUnhandledOutboxPublisherError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "OutboxKafkaPublisherService stopped (owner={LockOwner})")]
    private static partial void LogOutboxPublisherStopped(ILogger logger, string lockOwner);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Debug, Message = "Claimed {Count} outbox messages (owner={LockOwner}, parallelism={Parallelism})")]
    private static partial void LogOutboxMessagesClaimed(ILogger logger, int count, string lockOwner, int parallelism);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Warning, Message = "Outbox message skipped because lock is not owned or expired (currentOwner={CurrentOwner}, expectedOwner={ExpectedOwner}, lockedUntil={LockedUntil})")]
    private static partial void LogOutboxMessageSkippedBecauseLockExpired(ILogger logger, string? currentOwner, string expectedOwner, DateTime? lockedUntil);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Debug, Message = "Outbox message marked as SENT")]
    private static partial void LogOutboxMessageMarkedAsSent(ILogger logger);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Warning, Message = "Falha ao publicar outbox message. Proxima tentativa em {NextAttemptAt}")]
    private static partial void LogOutboxPublishFailed(ILogger logger, Exception exception, DateTime nextAttemptAt);
}
