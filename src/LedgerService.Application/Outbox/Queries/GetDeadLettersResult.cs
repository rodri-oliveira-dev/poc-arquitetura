namespace LedgerService.Application.Outbox.Queries;

public sealed record GetDeadLettersResult(
    IReadOnlyList<DeadLetterOutboxMessageDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
