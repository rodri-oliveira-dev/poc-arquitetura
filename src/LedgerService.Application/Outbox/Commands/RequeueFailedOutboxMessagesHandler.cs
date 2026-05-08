using LedgerService.Domain.Repositories;
using MediatR;

namespace LedgerService.Application.Outbox.Commands;

public sealed class RequeueFailedOutboxMessagesHandler
    : IRequestHandler<RequeueFailedOutboxMessagesCommand, RequeueFailedOutboxMessagesResult>
{
    private readonly IOutboxMessageRepository _outboxMessageRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RequeueFailedOutboxMessagesHandler(
        IOutboxMessageRepository outboxMessageRepository,
        IUnitOfWork unitOfWork)
    {
        _outboxMessageRepository = outboxMessageRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RequeueFailedOutboxMessagesResult> Handle(
        RequeueFailedOutboxMessagesCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requeuedAt = DateTime.Now;

        var requeued = await _outboxMessageRepository.RequeueFailedAsync(
            request.OutboxMessageId,
            request.EventType,
            request.OccurredFrom,
            request.OccurredUntil,
            request.Limit,
            requeuedAt,
            request.RequeuedBy,
            request.Reason,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RequeueFailedOutboxMessagesResult(
            requeued.Count,
            requeued.Select(x => x.Id).ToArray());
    }
}
