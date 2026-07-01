using AuditService.Domain.FunctionalAuditing;
using AuditService.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace AuditService.Infrastructure.Tests.Persistence;

public sealed class AuditDbContextModelTests
{
    [Fact]
    public void Model_should_configure_functional_audit_records_for_audit_schema()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(FunctionalAuditRecord));

        Assert.NotNull(entityType);
        Assert.Equal("audit", entityType.GetSchema());
        Assert.Equal("functional_audit_records", entityType.GetTableName());
        Assert.Equal("jsonb", entityType.FindProperty(nameof(FunctionalAuditRecord.Metadata))?.GetColumnType());
        Assert.True(entityType.FindProperty(nameof(FunctionalAuditRecord.OperationId))?.IsNullable == false);
        Assert.True(entityType.FindProperty(nameof(FunctionalAuditRecord.SourceService))?.IsNullable == false);
        Assert.True(entityType.FindProperty(nameof(FunctionalAuditRecord.OperationType))?.IsNullable == false);
        Assert.True(entityType.FindProperty(nameof(FunctionalAuditRecord.Status))?.IsNullable == false);
        Assert.True(entityType.FindProperty(nameof(FunctionalAuditRecord.OccurredAt))?.IsNullable == false);
    }

    [Theory]
    [InlineData("idx_audit_functional_audit_records_operation_id")]
    [InlineData("idx_audit_functional_audit_records_correlation_id")]
    [InlineData("ux_audit_functional_audit_records_idempotency_key")]
    [InlineData("idx_audit_functional_audit_records_merchant_occurred_at")]
    [InlineData("idx_audit_functional_audit_records_source_operation")]
    [InlineData("idx_audit_functional_audit_records_entity")]
    public void Model_should_configure_required_indexes(string indexName)
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(FunctionalAuditRecord));

        Assert.NotNull(entityType);
        Assert.Contains(entityType.GetIndexes(), index => index.GetDatabaseName() == indexName);
    }

    [Fact]
    public void Model_should_configure_idempotency_key_as_unique()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(FunctionalAuditRecord));

        Assert.NotNull(entityType);
        var index = Assert.Single(
            entityType.GetIndexes(),
            index => index.GetDatabaseName() == "ux_audit_functional_audit_records_idempotency_key");
        Assert.True(index.IsUnique);
    }

    private static AuditDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=audit_tests;Username=test")
            .Options;

        return new AuditDbContext(options);
    }
}
