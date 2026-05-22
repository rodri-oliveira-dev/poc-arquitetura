using Microsoft.Extensions.Logging;

namespace LedgerService.Worker.Observability;

internal static partial class OutboxKafkaPublisherLogMessages
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "OutboxKafkaPublisherService started (owner={LockOwner}, interval={IntervalSeconds}s)")]
    internal static partial void PublisherStarted(this ILogger logger, string lockOwner, double intervalSeconds);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "Erro persistente no OutboxKafkaPublisherService. Vai retentar no proximo ciclo.")]
    internal static partial void PersistentPublisherError(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "Timeout no OutboxKafkaPublisherService. Vai retentar no proximo ciclo.")]
    internal static partial void PublisherTimeout(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Error, Message = "Erro não tratado no OutboxKafkaPublisherService. Vai retentar no próximo ciclo.")]
    internal static partial void UnhandledPublisherError(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "OutboxKafkaPublisherService stopped (owner={LockOwner})")]
    internal static partial void PublisherStopped(this ILogger logger, string lockOwner);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Debug, Message = "Claimed {Count} outbox messages (owner={LockOwner}, parallelism={Parallelism})")]
    internal static partial void OutboxMessagesClaimed(this ILogger logger, int count, string lockOwner, int parallelism);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Warning, Message = "Outbox message skipped because lock is not owned or expired (currentOwner={CurrentOwner}, expectedOwner={ExpectedOwner}, lockedUntil={LockedUntil})")]
    internal static partial void OutboxMessageSkippedBecauseLockExpired(this ILogger logger, string? currentOwner, string expectedOwner, DateTime? lockedUntil);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Debug, Message = "Outbox message marked as Processed")]
    internal static partial void OutboxMessageMarkedAsProcessed(this ILogger logger);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Warning, Message = "Falha ao publicar outbox message. Proxima tentativa em {NextRetryAt}")]
    internal static partial void OutboxPublishFailed(this ILogger logger, Exception exception, DateTime nextRetryAt);

    [LoggerMessage(EventId = 1009, Level = LogLevel.Critical, Message = "Outbox message {OutboxId} movida para DeadLetter apos exceder limite de retries.")]
    internal static partial void OutboxMessageMovedToDeadLetter(this ILogger logger, Exception exception, Guid outboxId);
}
