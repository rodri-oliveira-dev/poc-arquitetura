using MediatR;

namespace LedgerService.Application.Outbox.Queries;

public sealed record GetDeadLettersQuery(int Page, int PageSize) : IRequest<GetDeadLettersResult>;
