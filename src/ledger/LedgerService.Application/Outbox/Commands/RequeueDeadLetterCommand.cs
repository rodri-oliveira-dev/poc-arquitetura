using MediatR;

namespace LedgerService.Application.Outbox.Commands;

public sealed record RequeueDeadLetterCommand(
    Guid OutboxMessageId,
    string Reason,
    string RequeuedBy) : IRequest<RequeueDeadLetterResult>;
