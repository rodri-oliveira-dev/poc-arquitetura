using MediatR;

namespace PaymentService.Application.Payments.InboxProcessing;

public sealed record ProcessPaymentInboxMessageCommand(Guid InboxMessageId, string LockOwner)
    : IRequest<ProcessPaymentInboxMessageResult>;
