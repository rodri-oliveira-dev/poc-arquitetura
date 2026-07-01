using System.Text;
using System.Text.Json;

using AuditService.Domain.FunctionalAuditing;
using AuditService.Worker.Messaging.Kafka.Contracts;

using FluentValidation;

namespace AuditService.Worker.Messaging.Kafka;

internal sealed class AuditRecordRequestedValidator : AbstractValidator<AuditRecordRequestedEvent>
{
    public const string EventType = "AuditRecordRequested.v1";
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AuditRecordRequestedValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty();

        RuleFor(x => x.EventType)
            .Equal(EventType);

        RuleFor(x => x.SchemaVersion)
            .Equal(SchemaVersion);

        RuleFor(x => x.OperationId)
            .NotEmpty();

        RuleFor(x => x.SourceService)
            .NotEmpty()
            .MaximumLength(FunctionalAuditRecord.SourceServiceMaxLength);

        RuleFor(x => x.OperationType)
            .NotEmpty()
            .MaximumLength(FunctionalAuditRecord.OperationTypeMaxLength);

        RuleFor(x => x.Status)
            .NotEmpty();

        RuleFor(x => x.OccurredAt)
            .NotEmpty();

        RuleFor(x => x.Metadata)
            .Must(FitMetadataLimit)
            .WithMessage($"Metadata must be at most {FunctionalAuditRecord.MetadataMaxBytes} bytes.");
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
