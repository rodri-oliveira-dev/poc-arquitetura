using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var externalReferenceConverter = new ValueConverter<ExternalReference?, string?>(
            value => value.HasValue ? value.Value.Value : null,
            value => value == null ? null : new ExternalReference(value));
        var externalPaymentReferenceConverter = new ValueConverter<ExternalPaymentReference?, string?>(
            value => value.HasValue ? value.Value.Value : null,
            value => value == null ? null : new ExternalPaymentReference(value));
        var ledgerEntryReferenceConverter = new ValueConverter<LedgerEntryReference?, Guid?>(
            value => value.HasValue ? value.Value.Value : null,
            value => value.HasValue ? new LedgerEntryReference(value.Value) : null);

        builder.ToTable("payments");

        builder.Ignore(x => x.Id);
        builder.Ignore(x => x.Amount);

        builder.HasKey(x => x.PaymentId);

        builder.Property(x => x.PaymentId)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new PaymentId(value))
            .ValueGeneratedNever();

        builder.Property(x => x.MerchantId)
            .HasColumnName("merchant_id")
            .HasConversion(id => id.Value, value => new MerchantId(value))
            .HasMaxLength(MerchantId.MaxLength)
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

        builder.Property(x => x.Provider)
            .HasColumnName("provider")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(Payment.DescriptionMaxLength);

        builder.Property(x => x.ExternalReference)
            .HasColumnName("external_reference")
            .HasConversion(externalReferenceConverter)
            .HasMaxLength(ExternalReference.MaxLength);

        builder.Property(x => x.ExternalPaymentReference)
            .HasColumnName("external_payment_reference")
            .HasConversion(externalPaymentReferenceConverter)
            .HasMaxLength(ExternalPaymentReference.MaxLength);

        builder.Property(x => x.LedgerEntryReference)
            .HasColumnName("ledger_entry_id")
            .HasConversion(ledgerEntryReferenceConverter);

        builder.Property(x => x.LedgerIntegrationStatus)
            .HasColumnName("ledger_integration_status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.LedgerIntegrationAttemptCount)
            .HasColumnName("ledger_integration_attempt_count")
            .IsRequired();

        builder.Property(x => x.LedgerNextRetryAt)
            .HasColumnName("ledger_next_retry_at_utc")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.LedgerLastError)
            .HasColumnName("ledger_last_error")
            .HasMaxLength(1000);

        builder.Property(x => x.LedgerProcessingStartedAt)
            .HasColumnName("ledger_processing_started_at_utc")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.LedgerLockedUntil)
            .HasColumnName("ledger_locked_until_utc")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.LedgerLockOwner)
            .HasColumnName("ledger_lock_owner")
            .HasMaxLength(200);

        builder.Property(x => x.LedgerCorrelationId)
            .HasColumnName("ledger_correlation_id")
            .HasMaxLength(100);

        builder.Property(x => x.ProviderStatus)
            .HasColumnName("provider_status")
            .HasMaxLength(Payment.ProviderStatusMaxLength);

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

        builder.HasIndex(x => new { x.MerchantId, x.ExternalReference })
            .HasDatabaseName("idx_payment_payments_merchant_external_reference")
            .HasFilter("external_reference IS NOT NULL");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("idx_payment_payments_status");

        builder.HasIndex(x => new { x.Status, x.LedgerIntegrationStatus, x.LedgerNextRetryAt, x.LedgerLockedUntil })
            .HasDatabaseName("idx_payment_payments_ledger_claim");

        builder.HasIndex(x => new { x.Provider, x.ExternalPaymentReference })
            .IsUnique()
            .HasDatabaseName("ux_payment_payments_provider_external_reference")
            .HasFilter("external_payment_reference IS NOT NULL");
    }
}
