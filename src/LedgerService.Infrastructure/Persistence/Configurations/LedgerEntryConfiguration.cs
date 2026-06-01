using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LedgerService.Domain.Entities;

namespace LedgerService.Infrastructure.Persistence.Configurations;

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("ledger_entries", table =>
        {
            table.HasCheckConstraint(
                "ck_ledger_entries_amount_credit_debit",
                "(type = 'Credit' AND amount > 0) OR (type = 'Debit' AND amount < 0)"
            );
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.MerchantId)
            .HasColumnName("merchant_id")
            .IsRequired();

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(x => x.ExternalReference)
            .HasColumnName("external_reference")
            .HasMaxLength(150);

        builder.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.HasIndex(x => new { x.MerchantId, x.OccurredAt })
            .HasDatabaseName("idx_ledger_entries_merchant_occurred_at");

        builder.HasIndex(x => x.ExternalReference)
            .HasDatabaseName("ux_ledger_entries_estorno_external_reference")
            .IsUnique()
            .HasFilter("external_reference LIKE 'estorno:%'");
    }
}
