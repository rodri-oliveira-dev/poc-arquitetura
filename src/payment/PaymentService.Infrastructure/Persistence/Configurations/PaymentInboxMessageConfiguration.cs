using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using PaymentService.Application.Payments.Webhooks;
using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Persistence.Configurations;

public sealed class PaymentInboxMessageConfiguration : IEntityTypeConfiguration<PaymentInboxMessage>
{
    public void Configure(EntityTypeBuilder<PaymentInboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var paymentIdConverter = new ValueConverter<PaymentId?, Guid?>(
            value => value.HasValue ? value.Value.Value : null,
            value => value.HasValue ? new PaymentId(value.Value) : null);

        builder.ToTable("inbox_messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.Provider)
            .HasColumnName("provider")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ProviderEventId)
            .HasColumnName("provider_event_id")
            .HasMaxLength(PaymentInboxMessage.ProviderEventIdMaxLength)
            .IsRequired();

        builder.Property(x => x.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(PaymentInboxMessage.EventTypeMaxLength)
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasColumnName("payload")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.PayloadSha256)
            .HasColumnName("payload_sha256")
            .HasMaxLength(PaymentInboxMessage.HashMaxLength)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.EventCategory)
            .HasColumnName("event_category")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ReceivedAt)
            .HasColumnName("received_at_utc")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.Property(x => x.ProcessedAt)
            .HasColumnName("processed_at_utc")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.AttemptCount)
            .HasColumnName("attempt_count")
            .IsRequired();

        builder.Property(x => x.NextRetryAt)
            .HasColumnName("next_retry_at_utc")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(PaymentInboxMessage.LastErrorMaxLength);

        builder.Property(x => x.ProcessingStartedAt)
            .HasColumnName("processing_started_at_utc")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.LockOwner)
            .HasColumnName("lock_owner")
            .HasMaxLength(PaymentInboxMessage.LockOwnerMaxLength);

        builder.Property(x => x.LockedUntil)
            .HasColumnName("locked_until_utc")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(PaymentInboxMessage.CorrelationIdMaxLength);

        builder.Property(x => x.ProviderPaymentId)
            .HasColumnName("provider_payment_id")
            .HasMaxLength(PaymentInboxMessage.ProviderPaymentIdMaxLength);

        builder.Property(x => x.PaymentId)
            .HasColumnName("payment_id")
            .HasConversion(paymentIdConverter);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.HasIndex(x => new { x.Provider, x.ProviderEventId })
            .IsUnique()
            .HasDatabaseName("ux_payment_inbox_provider_event");

        builder.HasIndex(x => new { x.Status, x.NextRetryAt })
            .HasDatabaseName("idx_payment_inbox_status_next_retry");

        builder.HasIndex(x => x.ReceivedAt)
            .HasDatabaseName("idx_payment_inbox_received_at");
    }
}
