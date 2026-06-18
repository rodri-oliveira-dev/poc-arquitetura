using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using TransferService.Application.Abstractions.Persistence;
using TransferService.Application.Abstractions.Time;
using TransferService.Infrastructure.Persistence;
using TransferService.Infrastructure.Persistence.Outbox;
using TransferService.Worker.Messaging;
using TransferService.Worker.Options;

namespace TransferService.Worker.Outbox;

public sealed class TransferenciaOutboxPublisherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<TransferWorkerOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<TransferenciaOutboxPublisherService> _logger;
    private readonly string _lockOwner;

    public TransferenciaOutboxPublisherService(
        IServiceProvider serviceProvider,
        IOptions<TransferWorkerOptions> options,
        IClock clock,
        ILogger<TransferenciaOutboxPublisherService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _clock = clock;
        _logger = logger;
        _lockOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro nao tratado na publicacao da Outbox do TransferService.");
            }

            await Task.Delay(_options.Value.PollingInterval, stoppingToken);
        }
    }

    public async Task PublishOnceAsync(CancellationToken cancellationToken)
    {
        if (!_options.Value.Enabled)
            return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransferServiceDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var producer = scope.ServiceProvider.GetRequiredService<ITransferenciaKafkaProducer>();

        var now = _clock.UtcNow;
        var lockedUntil = now.Add(_options.Value.LockDuration);
        var messages = await db.OutboxMessages
            .Where(x =>
                (x.Status == TransferenciaOutboxStatus.Pending || x.Status == TransferenciaOutboxStatus.Processing) &&
                (x.NextRetryAt == null || x.NextRetryAt <= now) &&
                (x.LockedUntil == null || x.LockedUntil <= now))
            .OrderBy(x => x.CreatedAt)
            .Take(_options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.MarkProcessing(_lockOwner, lockedUntil);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await PublishMessageAsync(message.Id, cancellationToken);
        }
    }

    private async Task PublishMessageAsync(Guid messageId, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransferServiceDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var producer = scope.ServiceProvider.GetRequiredService<ITransferenciaKafkaProducer>();

        var message = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);
        if (message is null || message.Status == TransferenciaOutboxStatus.Published)
            return;

        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = message.CorrelationId,
            ["TransferenciaId"] = message.AggregateId,
            ["OutboxId"] = message.Id,
            ["EventType"] = message.EventType
        });

        try
        {
            ValidatePayload(message);
            await producer.PublishAsync(message, message.Topic, cancellationToken);
            message.MarkPublished(_clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Outbox de transferencia publicada no Kafka. OutboxId={OutboxId} TransferenciaId={TransferenciaId} CorrelationId={CorrelationId} EventType={EventType} Topic={Topic}",
                message.Id,
                message.AggregateId,
                message.CorrelationId,
                message.EventType,
                message.Topic);
        }
        catch (JsonException ex)
        {
            await SendToDlqAsync(producer, message, ex.ToString(), unitOfWork, cancellationToken);
        }
        catch (TransferenciaKafkaPublishException ex) when (ex.IsTransient)
        {
            var retryCount = message.RetryCount + 1;
            if (retryCount >= _options.Value.MaxRetryCount)
            {
                await SendToDlqAsync(producer, message, ex.ToString(), unitOfWork, cancellationToken);
                return;
            }

            var nextRetryAt = _clock.UtcNow.Add(_options.Value.RetryBackoff);
            message.MarkFailedPublishAttempt(_options.Value.MaxRetryCount, nextRetryAt, ex.ToString());
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (TransferenciaKafkaPublishException ex)
        {
            await SendToDlqAsync(producer, message, ex.ToString(), unitOfWork, cancellationToken);
        }
    }

    private async Task SendToDlqAsync(
        ITransferenciaKafkaProducer producer,
        TransferenciaOutboxMessage message,
        string reason,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        await producer.PublishDlqAsync(message, reason, _options.Value.DlqTopic, cancellationToken);
        message.MarkDeadLetter(reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void ValidatePayload(TransferenciaOutboxMessage message)
    {
        using var _ = JsonDocument.Parse(message.Payload);
    }
}
