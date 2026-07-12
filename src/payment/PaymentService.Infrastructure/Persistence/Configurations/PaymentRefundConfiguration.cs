using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Persistence.Configurations;

public sealed class PaymentRefundConfiguration : IEntityTypeConfiguration<PaymentRefund>
{
    public void Configure(EntityTypeBuilder<PaymentRefund> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var paymentIdConverter = new ValueConverter<PaymentId, Guid>(
            value => value.Value,
            value => new PaymentId(value));

        var refundIdConverter = new ValueConverter<RefundId, Guid>(
            value => value.Value,
            value => new RefundId(value));

        builder.ToTable("payment_refunds");

        builder.Ignore(x => x.Id);
        builder.Ignore(x => x.Amount);

        builder.HasKey(x => x.RefundId);

        builder.Property(x => x.RefundId)
            .HasColumnName("id")
            .HasConversion(refundIdConverter)
            .ValueGeneratedNever();

        builder.Property(x => x.PaymentId)
            .HasColumnName("payment_id")
            .HasConversion(paymentIdConverter)
            .IsRequired();

        builder.Property(x => x.AmountValue)
            .HasColumnName("amount")
            .HasColumnType(PostgreSqlColumnTypes.Numeric18And2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasColumnName("currency")
            .HasConversion(currency => currency.Code, value => new Currency(value))
            .HasMaxLength(Currency.CodeLength)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(PaymentRefund.ReasonMaxLength)
            .IsRequired();

        builder.Property(x => x.ExternalReference)
            .HasColumnName("external_reference")
            .HasMaxLength(PaymentRefund.ExternalReferenceMaxLength);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ProviderRefundId)
            .HasColumnName("provider_refund_id")
            .HasMaxLength(PaymentRefund.ProviderRefundIdMaxLength);

        builder.Property(x => x.ProviderStatus)
            .HasColumnName("provider_status")
            .HasMaxLength(PaymentRefund.ProviderStatusMaxLength);

        builder.Property(x => x.LedgerReversalId)
            .HasColumnName("ledger_reversal_id");

        builder.Property(x => x.LedgerReversalStatus)
            .HasColumnName("ledger_reversal_status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.LedgerReversalAttemptCount)
            .HasColumnName("ledger_reversal_attempt_count")
            .IsRequired();

        builder.Property(x => x.LedgerNextRetryAt)
            .HasColumnName("ledger_next_retry_at_utc")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.LedgerLastError)
            .HasColumnName("ledger_last_error")
            .HasMaxLength(PaymentRefund.LastErrorMaxLength);

        builder.Property(x => x.LedgerProcessingStartedAt)
            .HasColumnName("ledger_processing_started_at_utc")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.LedgerLockedUntil)
            .HasColumnName("ledger_locked_until_utc")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.LedgerLockOwner)
            .HasColumnName("ledger_lock_owner")
            .HasMaxLength(PaymentRefund.LockOwnerMaxLength);

        builder.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(100);

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

        builder.HasIndex(x => new { x.PaymentId, x.Status })
            .HasDatabaseName("idx_payment_refunds_payment_status");

        builder.HasIndex(x => x.ProviderRefundId)
            .IsUnique()
            .HasDatabaseName("ux_payment_refunds_provider_refund_id")
            .HasFilter("provider_refund_id IS NOT NULL");

        builder.HasIndex(x => new { x.Status, x.LedgerReversalStatus, x.LedgerNextRetryAt, x.LedgerLockedUntil })
            .HasDatabaseName("idx_payment_refunds_ledger_claim");
    }
}
