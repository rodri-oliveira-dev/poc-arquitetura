namespace LedgerService.Application.Outbox.Queries;

public sealed record DeadLetterOutboxMessageDto(
    Guid Id,
    string AggregateType,
    Guid AggregateId,
    string EventType,
    DateTime OccurredAt,
    int RetryCount,
    string? LastError,
    Guid? CorrelationId,
    string? TraceParent);
