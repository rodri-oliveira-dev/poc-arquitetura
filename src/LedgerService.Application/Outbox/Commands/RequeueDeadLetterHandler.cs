using LedgerService.Domain.Repositories;
using MediatR;

namespace LedgerService.Application.Outbox.Commands;

public sealed class RequeueDeadLetterHandler : IRequestHandler<RequeueDeadLetterCommand, RequeueDeadLetterResult>
{
    private readonly IOutboxMessageRepository _outboxMessageRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RequeueDeadLetterHandler(
        IOutboxMessageRepository outboxMessageRepository,
        IUnitOfWork unitOfWork)
    {
        _outboxMessageRepository = outboxMessageRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RequeueDeadLetterResult> Handle(
        RequeueDeadLetterCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requeued = await _outboxMessageRepository.RequeueDeadLettersAsync(
            request.OutboxMessageId,
            eventType: null,
            occurredFrom: null,
            occurredUntil: null,
            limit: 1,
            requeuedAt: DateTime.Now,
            requeuedBy: request.RequeuedBy,
            reason: request.Reason,
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RequeueDeadLetterResult(requeued.Count == 1, request.OutboxMessageId);
    }
}
