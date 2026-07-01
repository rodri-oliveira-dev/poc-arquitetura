using System.Globalization;

using AuditService.Application.FunctionalAuditing.CreateAuditRecord;
using AuditService.Domain.FunctionalAuditing;

namespace AuditService.Application.Tests.FunctionalAuditing;

public sealed class CreateAuditRecordCommandValidatorTests
{
    private readonly CreateAuditRecordCommandValidator _validator = new();

    [Fact]
    public void Validate_should_accept_valid_command()
    {
        var result = _validator.Validate(ValidCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_should_reject_missing_operation_id()
    {
        var result = _validator.Validate(ValidCommand() with
        {
            OperationId = Guid.Empty
        });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateAuditRecordCommand.OperationId));
    }

    [Fact]
    public void Validate_should_reject_invalid_idempotency_key()
    {
        var result = _validator.Validate(ValidCommand() with
        {
            IdempotencyKey = "invalid"
        });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateAuditRecordCommand.IdempotencyKey));
    }

    [Fact]
    public void Validate_should_reject_source_service_above_limit()
    {
        var result = _validator.Validate(ValidCommand() with
        {
            SourceService = new string('a', FunctionalAuditRecord.SourceServiceMaxLength + 1)
        });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateAuditRecordCommand.SourceService));
    }

    [Fact]
    public void Validate_should_reject_operation_type_above_limit()
    {
        var result = _validator.Validate(ValidCommand() with
        {
            OperationType = new string('a', FunctionalAuditRecord.OperationTypeMaxLength + 1)
        });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateAuditRecordCommand.OperationType));
    }

    [Fact]
    public void Validate_should_reject_reason_above_limit()
    {
        var result = _validator.Validate(ValidCommand() with
        {
            Reason = new string('a', FunctionalAuditRecord.ReasonMaxLength + 1)
        });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateAuditRecordCommand.Reason));
    }

    [Fact]
    public void Validate_should_reject_unsupported_actor_type()
    {
        var result = _validator.Validate(ValidCommand() with
        {
            Actor = new CreateAuditRecordActor("Ledger", "subject", null)
        });

        Assert.Contains(result.Errors, error => error.PropertyName == "Actor.Type");
    }

    [Fact]
    public void Validate_should_reject_metadata_above_limit()
    {
        var result = _validator.Validate(ValidCommand() with
        {
            Metadata = new Dictionary<string, string>
            {
                ["oversized"] = new('a', FunctionalAuditRecord.MetadataMaxBytes)
            }
        });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateAuditRecordCommand.Metadata));
    }

    private static CreateAuditRecordCommand ValidCommand()
        => new(
            OperationId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CorrelationId: Guid.Parse("00000000-0000-0000-0000-000000000002"),
            IdempotencyKey: "00000000-0000-0000-0000-000000000003",
            SourceService: "AnyCallingService",
            OperationType: "AnyOperationCompleted",
            EntityType: "AnyEntity",
            EntityId: "any-123",
            MerchantId: "m1",
            Actor: new CreateAuditRecordActor("Client", "poc-automation", "poc-automation"),
            Status: "Succeeded",
            Reason: null,
            Metadata: new Dictionary<string, string>
            {
                ["key"] = "value"
            },
            OccurredAt: DateTimeOffset.Parse("2026-06-30T10:30:00Z", CultureInfo.InvariantCulture));
}
