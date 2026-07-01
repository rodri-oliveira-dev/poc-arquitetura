using IdentityService.Application.Idempotency;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("idempotency_records", table =>
        {
            table.HasCheckConstraint(
                "ck_identity_idempotency_records_status",
                "status IN ('Processing', 'Completed', 'Failed', 'Expired')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.OperationName)
            .HasColumnName("operation_name")
            .HasMaxLength(IdempotencyRecord.OperationNameMaxLength)
            .IsRequired();

        builder.Property(x => x.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(IdempotencyRecord.IdempotencyKeyMaxLength)
            .IsRequired();

        builder.Property(x => x.RequestHash)
            .HasColumnName("request_hash")
            .HasMaxLength(IdempotencyRecord.RequestHashMaxLength)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(IdempotencyRecord.StatusMaxLength)
            .IsRequired();

        builder.Property(x => x.ResponseStatusCode)
            .HasColumnName("response_status_code");

        builder.Property(x => x.ResponseBody)
            .HasColumnName("response_body")
            .HasColumnType("jsonb");

        builder.Property(x => x.ResourceId)
            .HasColumnName("resource_id");

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc)
            .HasColumnName("completed_at_utc")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.Property(x => x.LockedUntilUtc)
            .HasColumnName("locked_until_utc")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.FailureStage)
            .HasColumnName("failure_stage")
            .HasMaxLength(IdempotencyRecord.FailureStageMaxLength);

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(IdempotencyRecord.ErrorMessageMaxLength);

        builder.HasIndex(x => new { x.OperationName, x.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ux_identity_idempotency_records_operation_key");

        builder.HasIndex(x => x.ExpiresAtUtc)
            .HasDatabaseName("idx_identity_idempotency_records_expires_at");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("idx_identity_idempotency_records_status");
    }
}
