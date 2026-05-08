using MediatR;

namespace LedgerService.Application.Outbox.Commands;

public sealed record RequeueFailedOutboxMessagesCommand(
    Guid? OutboxMessageId,
    string? EventType,
    DateTime? OccurredFrom,
    DateTime? OccurredUntil,
    int Limit,
    string Reason,
    string RequeuedBy) : IRequest<RequeueFailedOutboxMessagesResult>;
