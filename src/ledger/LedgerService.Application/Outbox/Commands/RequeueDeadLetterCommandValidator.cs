using FluentValidation;

namespace LedgerService.Application.Outbox.Commands;

public sealed class RequeueDeadLetterCommandValidator : AbstractValidator<RequeueDeadLetterCommand>
{
    public RequeueDeadLetterCommandValidator()
    {
        RuleFor(x => x.OutboxMessageId)
            .NotEmpty();

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.RequeuedBy)
            .NotEmpty()
            .MaximumLength(200);
    }
}
