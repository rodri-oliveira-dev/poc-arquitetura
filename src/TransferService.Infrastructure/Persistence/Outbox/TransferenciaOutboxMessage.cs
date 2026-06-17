namespace TransferService.Infrastructure.Persistence.Outbox;

public sealed class TransferenciaOutboxMessage
{
    public Guid Id { get; private set; }
    public string AggregateType { get; private set; }
    public Guid AggregateId { get; private set; }
    public string EventType { get; private set; }
    public string Payload { get; private set; }
    public string Topic { get; private set; }
    public string MessageKey { get; private set; }
    public string? CorrelationId { get; private set; }
    public TransferenciaOutboxStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public string? LockOwner { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }

    private TransferenciaOutboxMessage()
    {
        AggregateType = string.Empty;
        EventType = string.Empty;
        Payload = string.Empty;
        Topic = string.Empty;
        MessageKey = string.Empty;
    }

    public TransferenciaOutboxMessage(
        string aggregateType,
        Guid aggregateId,
        string eventType,
        string payload,
        string topic,
        string messageKey,
        string? correlationId,
        DateTimeOffset occurredAt,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageKey);

        Id = Guid.NewGuid();
        AggregateType = aggregateType.Trim();
        AggregateId = aggregateId;
        EventType = eventType.Trim();
        Payload = payload;
        Topic = topic.Trim();
        MessageKey = messageKey.Trim();
        CorrelationId = Normalize(correlationId);
        OccurredAt = occurredAt;
        CreatedAt = createdAt;
        Status = TransferenciaOutboxStatus.Pending;
    }

    public void MarkProcessing(string lockOwner, DateTimeOffset lockedUntil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockOwner);

        Status = TransferenciaOutboxStatus.Processing;
        LockOwner = lockOwner.Trim();
        LockedUntil = lockedUntil;
    }

    public void MarkPublished(DateTimeOffset publishedAt)
    {
        Status = TransferenciaOutboxStatus.Published;
        PublishedAt = publishedAt;
        LastError = null;
        NextRetryAt = null;
        LockOwner = null;
        LockedUntil = null;
    }

    public void MarkFailedPublishAttempt(int maxRetries, DateTimeOffset nextRetryAt, string? lastError)
    {
        RetryCount++;
        LastError = Normalize(lastError);
        LockOwner = null;
        LockedUntil = null;

        if (RetryCount >= maxRetries)
        {
            Status = TransferenciaOutboxStatus.DeadLetter;
            NextRetryAt = null;
            return;
        }

        Status = TransferenciaOutboxStatus.Pending;
        NextRetryAt = nextRetryAt;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
