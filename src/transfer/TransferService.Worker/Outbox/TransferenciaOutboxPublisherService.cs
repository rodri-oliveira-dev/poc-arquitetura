using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using TransferService.Application.Abstractions.Persistence;
using TransferService.Infrastructure.Persistence;
using TransferService.Infrastructure.Persistence.Outbox;
using TransferService.Worker.Messaging;
using TransferService.Worker.Options;

namespace TransferService.Worker.Outbox;

public sealed class TransferenciaOutboxPublisherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<TransferWorkerOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TransferenciaOutboxPublisherService> _logger;
    private readonly string _lockOwner;
    private static readonly System.Diagnostics.Metrics.Meter _workerMeter = new("TransferService.Worker");
    private static readonly System.Diagnostics.Metrics.Counter<long> _outboxPublishingErrors = _workerMeter.CreateCounter<long>(
        "transfer.worker.outbox.publishing.errors",
        unit: "1",
        description: "Total de erros no loop de publicacao da Outbox de transferencia por classificacao.");
    private static readonly Action<ILogger, Exception?> _logUnhandledOutboxPublishingError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1, nameof(_logUnhandledOutboxPublishingError)),
            "Erro nao tratado na publicacao da Outbox do TransferService.");
    private static readonly Action<ILogger, Guid, Guid, string?, string, string, Exception?> _logOutboxPublished =
        LoggerMessage.Define<Guid, Guid, string?, string, string>(
            LogLevel.Information,
            new EventId(2, nameof(_logOutboxPublished)),
            "Outbox de transferencia publicada no Kafka. OutboxId={OutboxId} TransferenciaId={TransferenciaId} CorrelationId={CorrelationId} EventType={EventType} Topic={Topic}");

    public TransferenciaOutboxPublisherService(
        IServiceProvider serviceProvider,
        IOptions<TransferWorkerOptions> options,
        TimeProvider timeProvider,
        ILogger<TransferenciaOutboxPublisherService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _timeProvider = timeProvider;
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
#pragma warning disable CA1031
            // Captura desconhecida intencional: protege o loop do publisher; falhas por mensagem ja sao tratadas em PublishMessageAsync.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                RecordOutboxPublishingError("unknown_loop_failure");
                _logUnhandledOutboxPublishingError(_logger, ex);
            }

            await Task.Delay(_options.Value.PollingInterval, _timeProvider, stoppingToken);
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

        var now = _timeProvider.GetUtcNow();
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
            message.MarkPublished(_timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            _logOutboxPublished(
                _logger,
                message.Id,
                message.AggregateId,
                message.CorrelationId,
                message.EventType,
                message.Topic,
                null);
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

            var nextRetryAt = _timeProvider.GetUtcNow().Add(_options.Value.RetryBackoff);
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

    private static void RecordOutboxPublishingError(string classification)
        => _outboxPublishingErrors.Add(
            1,
            new KeyValuePair<string, object?>("classification", classification));
}
