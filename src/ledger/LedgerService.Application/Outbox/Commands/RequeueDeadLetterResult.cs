namespace LedgerService.Application.Outbox.Commands;

public sealed record RequeueDeadLetterResult(bool Requeued, Guid OutboxMessageId);
