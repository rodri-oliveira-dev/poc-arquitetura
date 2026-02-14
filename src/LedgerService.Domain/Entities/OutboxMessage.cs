using LedgerService.Domain.Common;

namespace LedgerService.Domain.Entities;

public sealed class OutboxMessage : Entity
{
    public string AggregateType { get; private set; }
    public Guid AggregateId { get; private set; }
    public string EventType { get; private set; }
    public string Payload { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public OutboxStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? LastError { get; private set; }
    public Guid? CorrelationId { get; private set; }
    public DateTime? LockedUntil { get; private set; }
    public string? LockOwner { get; private set; }

    private OutboxMessage()
    {
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
        Guid? correlationId)
    {
        AggregateType = aggregateType;
        AggregateId = aggregateId;
        EventType = eventType;
        Payload = payload;
        OccurredAt = occurredAt;
        Status = OutboxStatus.Pending;
        Attempts = 0;
        CorrelationId = correlationId;
    }

    public void MarkProcessing(string lockOwner, DateTime lockedUntil)
    {
        Status = OutboxStatus.Processing;
        LockOwner = lockOwner;
        LockedUntil = lockedUntil;
    }

    public void MarkSent(DateTime processedAt)
    {
        Status = OutboxStatus.Sent;
        ProcessedAt = processedAt;
        LockedUntil = null;
        LockOwner = null;
        NextAttemptAt = null;
        LastError = null;
    }

    public void MarkFailedAttempt(int maxAttempts, DateTime nextAttemptAt, string? lastError)
    {
        Attempts++;
        LastError = lastError;
        LockedUntil = null;
        LockOwner = null;

        if (Attempts >= maxAttempts)
        {
            Status = OutboxStatus.Failed;
            NextAttemptAt = null;
            ProcessedAt = DateTime.Now;
            return;
        }

        Status = OutboxStatus.Pending;
        NextAttemptAt = nextAttemptAt;
    }
}