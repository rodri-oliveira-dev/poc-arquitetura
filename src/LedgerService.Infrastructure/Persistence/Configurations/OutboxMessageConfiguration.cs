using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LedgerService.Domain.Entities;

namespace LedgerService.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.AggregateType)
            .HasColumnName("aggregate_type")
            .IsRequired();

        builder.Property(x => x.AggregateId)
            .HasColumnName("aggregate_id")
            .IsRequired();

        builder.Property(x => x.EventType)
            .HasColumnName("event_type")
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.RetryCount)
            .HasColumnName("retry_count")
            .IsRequired();

        builder.Property(x => x.NextRetryAt)
            .HasColumnName("next_retry_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.ProcessedAt)
            .HasColumnName("processed_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.LastError)
            .HasColumnName("last_error");

        builder.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id");

        builder.Property(x => x.TraceParent)
            .HasColumnName("traceparent")
            .HasMaxLength(128);

        builder.Property(x => x.TraceState)
            .HasColumnName("tracestate")
            .HasMaxLength(512);

        builder.Property(x => x.Baggage)
            .HasColumnName("baggage")
            .HasMaxLength(1024);

        builder.Property(x => x.LockedUntil)
            .HasColumnName("locked_until")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.LockOwner)
            .HasColumnName("lock_owner");

        builder.Property(x => x.RequeueCount)
            .HasColumnName("requeue_count")
            .IsRequired();

        builder.Property(x => x.LastRequeuedAt)
            .HasColumnName("last_requeued_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.LastRequeuedBy)
            .HasColumnName("last_requeued_by")
            .HasMaxLength(200);

        builder.Property(x => x.LastRequeueReason)
            .HasColumnName("last_requeue_reason")
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.Status, x.NextRetryAt })
            .HasDatabaseName("idx_outbox_pending");

        builder.HasIndex(x => x.LockedUntil)
            .HasDatabaseName("idx_outbox_locked_until");
    }
}
