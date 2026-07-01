using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TransferService.Infrastructure.Persistence.Outbox;

namespace TransferService.Infrastructure.Persistence.Configurations;

public sealed class TransferenciaOutboxMessageConfiguration : IEntityTypeConfiguration<TransferenciaOutboxMessage>
{
    public void Configure(EntityTypeBuilder<TransferenciaOutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox_messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.AggregateType)
            .HasColumnName("aggregate_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.AggregateId)
            .HasColumnName("aggregate_id")
            .IsRequired();

        builder.Property(x => x.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.Topic)
            .HasColumnName("topic")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.MessageKey)
            .HasColumnName("message_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.RetryCount)
            .HasColumnName("retry_count")
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasColumnName("last_error");

        builder.Property(x => x.NextRetryAt)
            .HasColumnName("next_retry_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.Property(x => x.PublishedAt)
            .HasColumnName("published_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.LockOwner)
            .HasColumnName("lock_owner")
            .HasMaxLength(200);

        builder.Property(x => x.LockedUntil)
            .HasColumnName("locked_until")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.HasIndex(x => new { x.Status, x.NextRetryAt, x.LockedUntil })
            .HasDatabaseName("idx_transfer_outbox_pending");
    }
}
