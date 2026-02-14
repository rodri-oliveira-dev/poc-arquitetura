using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Messaging.Kafka;
using LedgerService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LedgerService.Infrastructure.Outbox;

public sealed class OutboxKafkaPublisherService : BackgroundService
{
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

        _logger.LogInformation(
            "OutboxKafkaPublisherService started (owner={LockOwner}, interval={IntervalSeconds}s)",
            _lockOwner,
            interval.TotalSeconds);

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro não tratado no OutboxKafkaPublisherService. Vai retentar no próximo ciclo.");
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

        _logger.LogInformation("OutboxKafkaPublisherService stopped (owner={LockOwner})", _lockOwner);
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

        _logger.LogInformation(
            "Claimed {Count} outbox messages (owner={LockOwner}, parallelism={Parallelism})",
            claimed.Count,
            _lockOwner,
            options.MaxParallelism);

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
            _logger.LogWarning(
                "Outbox message skipped because lock is not owned or expired (currentOwner={CurrentOwner}, expectedOwner={ExpectedOwner}, lockedUntil={LockedUntil})",
                message.LockOwner,
                _lockOwner,
                message.LockedUntil);
            return;
        }

        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = message.CorrelationId,
            ["OutboxId"] = message.Id,
            ["EventType"] = message.EventType,
            ["AggregateId"] = message.AggregateId
        });

        try
        {
            await producer.ProduceAsync(message, ct);

            // importante: só marca SENT após confirmação do ProduceAsync
            await repo.MarkSentAsync(message.Id, DateTime.Now, ct);
            await uow.SaveChangesAsync(ct);

            _logger.LogInformation("Outbox message marked as SENT");
        }
        catch (Exception ex)
        {
            var nextAttemptAt = ComputeNextAttempt(DateTime.Now, message.Attempts + 1, options.BaseBackoffSeconds);
            await repo.MarkFailedAttemptAsync(message.Id, options.MaxAttempts, nextAttemptAt, ex.Message, ct);
            await uow.SaveChangesAsync(ct);

            _logger.LogWarning(ex, "Falha ao publicar outbox message. Próxima tentativa em {NextAttemptAt}", nextAttemptAt);
        }
    }

    private static DateTime ComputeNextAttempt(DateTime now, int attemptNumber, int baseBackoffSeconds)
    {
        var baseDelay = TimeSpan.FromSeconds(Math.Max(1, baseBackoffSeconds));
        var exp = Math.Pow(2, Math.Min(10, Math.Max(0, attemptNumber - 1)));
        var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * exp);
        var jitterMs = Random.Shared.Next(0, 250);
        return now.Add(delay).AddMilliseconds(jitterMs);
    }
}
