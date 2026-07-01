using AuditService.Domain.FunctionalAuditing;

using FluentValidation;

namespace AuditService.Application.FunctionalAuditing.SearchAuditRecords;

public sealed class SearchAuditRecordsQueryValidator : AbstractValidator<SearchAuditRecordsQuery>
{
    public SearchAuditRecordsQueryValidator()
    {
        RuleFor(x => x.MerchantId)
            .MaximumLength(FunctionalAuditRecord.MerchantIdMaxLength);

        RuleFor(x => x.SourceService)
            .MaximumLength(FunctionalAuditRecord.SourceServiceMaxLength);

        RuleFor(x => x.OperationType)
            .MaximumLength(FunctionalAuditRecord.OperationTypeMaxLength);

        RuleFor(x => x.Status)
            .MaximumLength(50);

        RuleFor(x => x.EntityType)
            .MaximumLength(FunctionalAuditRecord.EntityTypeMaxLength);

        RuleFor(x => x.EntityId)
            .MaximumLength(FunctionalAuditRecord.EntityIdMaxLength);

        RuleFor(x => x.From)
            .NotNull()
            .WithMessage("from is required.");

        RuleFor(x => x.To)
            .NotNull()
            .WithMessage("to is required.");

        RuleFor(x => x)
            .Must(x => x.From is null || x.To is null || x.To >= x.From)
            .WithMessage("to must be greater than or equal to from.");

        RuleFor(x => x)
            .Must(x => x.From is null || x.To is null || x.To.Value - x.From.Value <= TimeSpan.FromDays(SearchAuditRecordsQuery.MaxIntervalDays))
            .WithMessage($"The query interval must be at most {SearchAuditRecordsQuery.MaxIntervalDays} days.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, SearchAuditRecordsQuery.MaxPageSize);
    }
}
