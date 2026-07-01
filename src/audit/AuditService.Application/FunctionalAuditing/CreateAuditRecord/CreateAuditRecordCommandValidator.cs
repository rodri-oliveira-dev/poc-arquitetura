using System.Text;
using System.Text.Json;

using AuditService.Domain.FunctionalAuditing;

using FluentValidation;

namespace AuditService.Application.FunctionalAuditing.CreateAuditRecord;

public sealed class CreateAuditRecordCommandValidator : AbstractValidator<CreateAuditRecordCommand>
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Received",
        "Succeeded",
        "Failed",
        "Rejected",
        "Replayed"
    };

    private static readonly HashSet<string> ValidActorTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "User",
        "Client",
        "System"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CreateAuditRecordCommandValidator()
    {
        RuleFor(x => x.OperationId)
            .NotEmpty();

        RuleFor(x => x.CorrelationId)
            .Must(value => value is null || value.Value != Guid.Empty)
            .WithMessage("CorrelationId must be a valid UUID when provided.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("Idempotency-Key is required.")
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("Idempotency-Key must be a valid UUID.");

        RuleFor(x => x.SourceService)
            .NotEmpty()
            .MaximumLength(FunctionalAuditRecord.SourceServiceMaxLength);

        RuleFor(x => x.OperationType)
            .NotEmpty()
            .MaximumLength(FunctionalAuditRecord.OperationTypeMaxLength);

        RuleFor(x => x.EntityType)
            .MaximumLength(FunctionalAuditRecord.EntityTypeMaxLength);

        RuleFor(x => x.EntityId)
            .MaximumLength(FunctionalAuditRecord.EntityIdMaxLength);

        RuleFor(x => x.MerchantId)
            .MaximumLength(FunctionalAuditRecord.MerchantIdMaxLength);

        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(status => !string.IsNullOrWhiteSpace(status) && ValidStatuses.Contains(status.Trim()))
            .WithMessage("Status is not supported for functional audit records.");

        RuleFor(x => x.Reason)
            .MaximumLength(FunctionalAuditRecord.ReasonMaxLength);

        RuleFor(x => x.OccurredAt)
            .NotEmpty();

        RuleFor(x => x.Metadata)
            .Must(FitMetadataLimit)
            .WithMessage($"Metadata must be at most {FunctionalAuditRecord.MetadataMaxBytes} bytes.");

        When(x => x.Actor is not null, () =>
        {
            RuleFor(x => x.Actor!.Type)
                .MaximumLength(FunctionalAuditRecord.ActorTypeMaxLength)
                .Must(type => string.IsNullOrWhiteSpace(type) || ValidActorTypes.Contains(type.Trim()))
                .WithMessage("Actor type is not supported for functional audit records.");

            RuleFor(x => x.Actor!.Subject)
                .MaximumLength(FunctionalAuditRecord.ActorSubjectMaxLength);

            RuleFor(x => x.Actor!.ClientId)
                .MaximumLength(FunctionalAuditRecord.ActorClientIdMaxLength);
        });
    }

    private static bool FitMetadataLimit(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null)
            return true;

        var ordered = metadata
            .OrderBy(static item => item.Key, StringComparer.Ordinal)
            .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal);

        string json = JsonSerializer.Serialize(ordered, JsonOptions);
        return Encoding.UTF8.GetByteCount(json) <= FunctionalAuditRecord.MetadataMaxBytes;
    }
}
