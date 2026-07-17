using LedgerService.Application.Abstractions.Messaging;
using LedgerService.Domain.Repositories;

using MediatR;

namespace LedgerService.Application.Outbox.Commands;

public sealed class RequeueDeadLetterHandler(
    IOutboxMessageRepository outboxMessageRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<RequeueDeadLetterCommand, RequeueDeadLetterResult>
{
    private readonly IOutboxMessageRepository _outboxMessageRepository = outboxMessageRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly TimeProvider _timeProvider = timeProvider;

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
            requeuedAt: _timeProvider.GetUtcNow().UtcDateTime,
            requeuedBy: request.RequeuedBy,
            reason: request.Reason,
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RequeueDeadLetterResult(requeued.Count == 1, request.OutboxMessageId);
    }
}
