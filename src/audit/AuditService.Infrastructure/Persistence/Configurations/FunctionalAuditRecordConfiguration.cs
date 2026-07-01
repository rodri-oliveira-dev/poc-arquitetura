using AuditService.Domain.FunctionalAuditing;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditService.Infrastructure.Persistence.Configurations;

public sealed class FunctionalAuditRecordConfiguration : IEntityTypeConfiguration<FunctionalAuditRecord>
{
    public void Configure(EntityTypeBuilder<FunctionalAuditRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("functional_audit_records");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.OperationId)
            .HasColumnName("operation_id")
            .HasMaxLength(FunctionalAuditRecord.OperationIdMaxLength)
            .IsRequired();

        builder.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(FunctionalAuditRecord.CorrelationIdMaxLength);

        builder.Property(x => x.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(FunctionalAuditRecord.IdempotencyKeyMaxLength);

        builder.Property(x => x.SourceService)
            .HasColumnName("source_service")
            .HasMaxLength(FunctionalAuditRecord.SourceServiceMaxLength)
            .IsRequired();

        builder.Property(x => x.OperationType)
            .HasColumnName("operation_type")
            .HasMaxLength(FunctionalAuditRecord.OperationTypeMaxLength)
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(FunctionalAuditRecord.EntityTypeMaxLength);

        builder.Property(x => x.EntityId)
            .HasColumnName("entity_id")
            .HasMaxLength(FunctionalAuditRecord.EntityIdMaxLength);

        builder.Property(x => x.MerchantId)
            .HasColumnName("merchant_id")
            .HasMaxLength(FunctionalAuditRecord.MerchantIdMaxLength);

        builder.Property(x => x.ActorType)
            .HasColumnName("actor_type")
            .HasMaxLength(FunctionalAuditRecord.ActorTypeMaxLength);

        builder.Property(x => x.ActorSubject)
            .HasColumnName("actor_subject")
            .HasMaxLength(FunctionalAuditRecord.ActorSubjectMaxLength);

        builder.Property(x => x.ActorClientId)
            .HasColumnName("actor_client_id")
            .HasMaxLength(FunctionalAuditRecord.ActorClientIdMaxLength);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(FunctionalAuditRecord.ReasonMaxLength);

        builder.Property<IReadOnlyDictionary<string, string>>(nameof(FunctionalAuditRecord.Metadata))
            .HasColumnName("metadata")
            .HasColumnType(PostgreSqlColumnTypes.Jsonb)
            .HasField("_metadata")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();

        builder.Property(x => x.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.HasIndex(x => x.OperationId)
            .HasDatabaseName("idx_audit_functional_audit_records_operation_id");

        builder.HasIndex(x => x.CorrelationId)
            .HasDatabaseName("idx_audit_functional_audit_records_correlation_id");

        builder.HasIndex(x => x.IdempotencyKey)
            .HasDatabaseName("ux_audit_functional_audit_records_idempotency_key")
            .IsUnique();

        builder.HasIndex(x => new { x.MerchantId, x.OccurredAt })
            .HasDatabaseName("idx_audit_functional_audit_records_merchant_occurred_at")
            .IsDescending(false, true);

        builder.HasIndex(x => new { x.SourceService, x.OperationType })
            .HasDatabaseName("idx_audit_functional_audit_records_source_operation");

        builder.HasIndex(x => new { x.EntityType, x.EntityId })
            .HasDatabaseName("idx_audit_functional_audit_records_entity");
    }
}
