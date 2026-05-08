namespace LedgerService.Application.Outbox.Commands;

public sealed record RequeueFailedOutboxMessagesResult(
    int RequeuedCount,
    IReadOnlyList<Guid> OutboxMessageIds);
