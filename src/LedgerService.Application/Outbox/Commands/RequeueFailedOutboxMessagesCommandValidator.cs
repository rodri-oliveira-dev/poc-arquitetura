using FluentValidation;

namespace LedgerService.Application.Outbox.Commands;

public sealed class RequeueFailedOutboxMessagesCommandValidator : AbstractValidator<RequeueFailedOutboxMessagesCommand>
{
    public RequeueFailedOutboxMessagesCommandValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.RequeuedBy)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.EventType)
            .MaximumLength(200);

        RuleFor(x => x)
            .Must(HasAtLeastOneFilter)
            .WithMessage("Informe outboxMessageId, eventType, occurredFrom ou occurredUntil para limitar o requeue.");

        RuleFor(x => x)
            .Must(x => x.OccurredFrom is null || x.OccurredUntil is null || x.OccurredFrom <= x.OccurredUntil)
            .WithMessage("occurredFrom deve ser menor ou igual a occurredUntil.");
    }

    private static bool HasAtLeastOneFilter(RequeueFailedOutboxMessagesCommand command)
        => command.OutboxMessageId is not null
            || !string.IsNullOrWhiteSpace(command.EventType)
            || command.OccurredFrom is not null
            || command.OccurredUntil is not null;
}
