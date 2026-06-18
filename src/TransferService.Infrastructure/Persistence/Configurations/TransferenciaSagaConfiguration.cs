using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TransferService.Domain.Sagas;

namespace TransferService.Infrastructure.Persistence.Configurations;

public sealed class TransferenciaSagaConfiguration : IEntityTypeConfiguration<TransferenciaSaga>
{
    public void Configure(EntityTypeBuilder<TransferenciaSaga> builder)
    {
        builder.ToTable("transferencias_sagas");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.SourceMerchantId)
            .HasColumnName("source_merchant_id")
            .HasConversion(id => id.Value, value => new MerchantId(value))
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DestinationMerchantId)
            .HasColumnName("destination_merchant_id")
            .HasConversion(id => id.Value, value => new MerchantId(value))
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasConversion(amount => amount.Value, value => new TransferAmount(value))
            .HasColumnType(PostgreSqlColumnTypes.Numeric18And2)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(x => x.ExternalReference)
            .HasColumnName("external_reference")
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Step)
            .HasColumnName("current_step")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.DebitLancamentoId)
            .HasColumnName("debit_lancamento_id");

        builder.Property(x => x.CreditLancamentoId)
            .HasColumnName("credit_lancamento_id");

        builder.Property(x => x.CompensationEstornoId)
            .HasColumnName("compensation_estorno_id");

        builder.Property(x => x.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(200);

        builder.Property(x => x.IdempotencyPayloadHash)
            .HasColumnName("idempotency_payload_hash")
            .HasMaxLength(128);

        builder.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(200);

        builder.Property(x => x.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(1000);

        builder.Property(x => x.RetryCount)
            .HasColumnName("retry_count")
            .IsRequired();

        builder.Property(x => x.NextRetryAt)
            .HasColumnName("next_retry_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.ProcessingLockOwner)
            .HasColumnName("processing_lock_owner")
            .HasMaxLength(200);

        builder.Property(x => x.ProcessingLockedUntil)
            .HasColumnName("processing_locked_until")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.DebitCreated)
            .HasColumnName("debit_created")
            .IsRequired();

        builder.Property(x => x.CreditCreated)
            .HasColumnName("credit_created")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.HasIndex(x => new { x.SourceMerchantId, x.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ux_transferencias_sagas_source_idempotency_key")
            .HasFilter("idempotency_key IS NOT NULL");

        builder.HasIndex(x => new { x.Status, x.NextRetryAt, x.ProcessingLockedUntil })
            .HasDatabaseName("idx_transferencias_sagas_worker_pending");
    }
}
