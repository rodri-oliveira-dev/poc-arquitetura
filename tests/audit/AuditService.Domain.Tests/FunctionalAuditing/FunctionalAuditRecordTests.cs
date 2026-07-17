using AuditService.Domain.Exceptions;
using AuditService.Domain.FunctionalAuditing;

namespace AuditService.Domain.Tests.FunctionalAuditing;

public sealed class FunctionalAuditRecordTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 06, 30, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Create_should_create_valid_functional_audit_record()
    {
        var occurredAt = new DateTimeOffset(2026, 06, 30, 10, 00, 00, TimeSpan.Zero);

        var record = FunctionalAuditRecord.Create(
            operationId: "op-123",
            sourceService: "AnyCallingService",
            operationType: "PaymentAuthorized",
            status: "Received",
            occurredAt: occurredAt,
            entityType: "Payment",
            entityId: "pay-123",
            merchantId: "merchant-123",
            actorType: "Client",
            actorClientId: "client-123",
            metadata: new Dictionary<string, string>
            {
                ["channel"] = "api"
            },
            createdAt: CreatedAt);

        Assert.NotEqual(Guid.Empty, record.Id);
        Assert.Equal("op-123", record.OperationId);
        Assert.Equal("AnyCallingService", record.SourceService);
        Assert.Equal("PaymentAuthorized", record.OperationType);
        Assert.Equal("Received", record.Status);
        Assert.Equal(occurredAt, record.OccurredAt);
        Assert.Equal(CreatedAt, record.CreatedAt);
        Assert.Equal("api", record.Metadata["channel"]);
    }

    [Fact]
    public void Create_should_reject_empty_source_service()
    {
        var exception = Assert.Throws<DomainException>(() =>
            FunctionalAuditRecord.Create(
                operationId: "op-123",
                sourceService: " ",
                operationType: "PaymentAuthorized",
                status: "Received",
                occurredAt: CreatedAt,
                createdAt: CreatedAt));

        Assert.Contains("SourceService", exception.Message);
    }

    [Fact]
    public void Create_should_reject_empty_operation_type()
    {
        var exception = Assert.Throws<DomainException>(() =>
            FunctionalAuditRecord.Create(
                operationId: "op-123",
                sourceService: "AnyCallingService",
                operationType: " ",
                status: "Received",
                occurredAt: CreatedAt,
                createdAt: CreatedAt));

        Assert.Contains("OperationType", exception.Message);
    }

    [Fact]
    public void Create_should_reject_invalid_status()
    {
        var exception = Assert.Throws<DomainException>(() =>
            FunctionalAuditRecord.Create(
                operationId: "op-123",
                sourceService: "AnyCallingService",
                operationType: "PaymentAuthorized",
                status: "Unknown",
                occurredAt: CreatedAt,
                createdAt: CreatedAt));

        Assert.Contains("Status", exception.Message);
    }

    [Fact]
    public void Create_should_reject_optional_values_above_limits()
    {
        var exception = Assert.Throws<DomainException>(() =>
            FunctionalAuditRecord.Create(
                operationId: "op-123",
                sourceService: "AnyCallingService",
                operationType: "PaymentAuthorized",
                status: "Received",
                occurredAt: CreatedAt,
                createdAt: CreatedAt,
                entityType: new string('a', FunctionalAuditRecord.EntityTypeMaxLength + 1)));

        Assert.Contains("EntityType", exception.Message);
    }

    [Fact]
    public void Create_should_preserve_operation_correlation_and_idempotency_identifiers()
    {
        var record = FunctionalAuditRecord.Create(
            operationId: "op-123",
            sourceService: "AnyCallingService",
            operationType: "PaymentAuthorized",
            status: "Succeeded",
            occurredAt: CreatedAt,
            correlationId: "corr-123",
            idempotencyKey: "idem-123",
            createdAt: CreatedAt);

        Assert.Equal("op-123", record.OperationId);
        Assert.Equal("corr-123", record.CorrelationId);
        Assert.Equal("idem-123", record.IdempotencyKey);
    }
}
