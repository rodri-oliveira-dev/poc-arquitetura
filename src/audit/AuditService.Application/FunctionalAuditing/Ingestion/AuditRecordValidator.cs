using FluentValidation;
using FluentValidation.Results;

namespace AuditService.Application.FunctionalAuditing.Ingestion;

public sealed class AuditRecordValidator : IAuditRecordValidator
{
    public void ValidateAndThrow(AuditRecordEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        List<ValidationFailure> failures = [];

        if (string.IsNullOrWhiteSpace(envelope.ContractName))
            failures.Add(new ValidationFailure(nameof(AuditRecordEnvelope.ContractName), "ContractName is required."));

        if (envelope.ContractVersion <= 0)
            failures.Add(new ValidationFailure(nameof(AuditRecordEnvelope.ContractVersion), "ContractVersion must be greater than zero."));

        if (string.IsNullOrWhiteSpace(envelope.IdempotencyKey))
        {
            failures.Add(new ValidationFailure(nameof(AuditRecordEnvelope.IdempotencyKey), "IdempotencyKey is required."));
        }
        else if (!Guid.TryParse(envelope.IdempotencyKey, out _))
        {
            failures.Add(new ValidationFailure(nameof(AuditRecordEnvelope.IdempotencyKey), "IdempotencyKey must be a valid UUID."));
        }

        if (envelope.Payload is null)
        {
            failures.Add(new ValidationFailure(nameof(AuditRecordEnvelope.Payload), "Payload is required."));
        }
        else
        {
            ValidatePayload(envelope.Payload, failures);
        }

        if (envelope.Metadata?.CorrelationId == Guid.Empty)
            failures.Add(new ValidationFailure(nameof(AuditMetadata.CorrelationId), "CorrelationId must be a valid UUID when provided."));

        if (failures.Count > 0)
            throw new ValidationException(failures);
    }

    private static void ValidatePayload(
        AuditRecordPayload payload,
        List<ValidationFailure> failures)
    {
        if (payload.OperationId == Guid.Empty)
            failures.Add(new ValidationFailure(nameof(AuditRecordPayload.OperationId), "OperationId is required."));

        if (string.IsNullOrWhiteSpace(payload.SourceService))
            failures.Add(new ValidationFailure(nameof(AuditRecordPayload.SourceService), "SourceService is required."));

        if (string.IsNullOrWhiteSpace(payload.OperationType))
            failures.Add(new ValidationFailure(nameof(AuditRecordPayload.OperationType), "OperationType is required."));

        if (string.IsNullOrWhiteSpace(payload.Status))
            failures.Add(new ValidationFailure(nameof(AuditRecordPayload.Status), "Status is required."));

        if (payload.OccurredAt == default)
            failures.Add(new ValidationFailure(nameof(AuditRecordPayload.OccurredAt), "OccurredAt is required."));
    }
}
