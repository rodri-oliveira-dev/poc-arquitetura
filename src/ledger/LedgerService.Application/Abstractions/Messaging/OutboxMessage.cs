namespace LedgerService.Application.Abstractions.Messaging;

public sealed class OutboxMessage
{
    public Guid Id
    {
        get; private set;
    } = Guid.NewGuid();

    public string AggregateType
    {
        get; private set;
    }
    public Guid AggregateId
    {
        get; private set;
    }
    public string EventType
    {
        get; private set;
    }
    public string Payload
    {
        get; private set;
    }
    public DateTime OccurredAt
    {
        get; private set;
    }
    public OutboxStatus Status
    {
        get; private set;
    }
    public int RetryCount
    {
        get; private set;
    }
    public DateTime? NextRetryAt
    {
        get; private set;
    }
    public DateTime? ProcessedAt
    {
        get; private set;
    }
    public string? LastError
    {
        get; private set;
    }
    public Guid? CorrelationId
    {
        get; private set;
    }
    public string? TraceParent
    {
        get; private set;
    }
    public string? TraceState
    {
        get; private set;
    }
    public string? Baggage
    {
        get; private set;
    }
    public DateTime? LockedUntil
    {
        get; private set;
    }
    public string? LockOwner
    {
        get; private set;
    }
    public int RequeueCount
    {
        get; private set;
    }
    public DateTime? LastRequeuedAt
    {
        get; private set;
    }
    public string? LastRequeuedBy
    {
        get; private set;
    }
    public string? LastRequeueReason
    {
        get; private set;
    }

    private OutboxMessage()
    {
        Id = Guid.Empty;
        AggregateType = string.Empty;
        EventType = string.Empty;
        Payload = string.Empty;
    }

    public OutboxMessage(
        string aggregateType,
        Guid aggregateId,
        string eventType,
        string payload,
        DateTime occurredAt,
        Guid? correlationId,
        string? traceParent = null,
        string? traceState = null,
        string? baggage = null)
    {
        AggregateType = aggregateType;
        AggregateId = aggregateId;
        EventType = eventType;
        Payload = payload;
        OccurredAt = occurredAt;
        Status = OutboxStatus.Pending;
        RetryCount = 0;
        CorrelationId = correlationId;
        TraceParent = Normalize(traceParent);
        TraceState = Normalize(traceState);
        Baggage = Normalize(baggage);
    }

    public void MarkProcessing(string lockOwner, DateTime lockedUntil)
    {
        Status = OutboxStatus.Processing;
        LockOwner = lockOwner;
        LockedUntil = lockedUntil;
    }

    public void MarkProcessed(DateTime processedAt)
    {
        Status = OutboxStatus.Processed;
        ProcessedAt = processedAt;
        LockedUntil = null;
        LockOwner = null;
        NextRetryAt = null;
        LastError = null;
    }

    public void MarkFailedPublishAttempt(int maxRetries, DateTime nextRetryAt, string? lastError)
    {
        RetryCount++;
        LastError = lastError;
        LockedUntil = null;
        LockOwner = null;

        if (RetryCount >= maxRetries)
        {
            Status = OutboxStatus.DeadLetter;
            NextRetryAt = null;
            return;
        }

        Status = OutboxStatus.Pending;
        NextRetryAt = nextRetryAt;
    }

    public void RequeueDeadLetter(DateTime requeuedAt, string requeuedBy, string reason)
    {
        if (Status != OutboxStatus.DeadLetter)
            throw new InvalidOperationException("Only dead-letter outbox messages can be requeued.");

        Status = OutboxStatus.Pending;
        RetryCount = 0;
        NextRetryAt = null;
        ProcessedAt = null;
        LockedUntil = null;
        LockOwner = null;
        LastError = null;
        RequeueCount++;
        LastRequeuedAt = requeuedAt;
        LastRequeuedBy = requeuedBy;
        LastRequeueReason = reason;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
