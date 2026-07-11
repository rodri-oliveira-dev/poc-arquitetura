using LedgerService.Application.Abstractions.Messaging;

using MediatR;

namespace LedgerService.Application.Outbox.Queries;

public sealed class GetDeadLettersHandler(IOutboxMessageRepository outboxMessageRepository) : IRequestHandler<GetDeadLettersQuery, GetDeadLettersResult>
{
    private readonly IOutboxMessageRepository _outboxMessageRepository = outboxMessageRepository;

    public async Task<GetDeadLettersResult> Handle(GetDeadLettersQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (items, totalCount) = await _outboxMessageRepository.GetDeadLettersAsync(
            request.Page,
            request.PageSize,
            cancellationToken);

        return new GetDeadLettersResult(
            [.. items.Select(x => new DeadLetterOutboxMessageDto(
                x.Id,
                x.AggregateType,
                x.AggregateId,
                x.EventType,
                x.OccurredAt,
                x.RetryCount,
                x.LastError,
                x.CorrelationId,
                x.TraceParent))],
            request.Page,
            request.PageSize,
            totalCount);
    }
}
