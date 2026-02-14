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
            table.HasCheckConstraint("ck_ledger_entries_amount_positive", "amount > 0");
            table.HasCheckConstraint("ck_ledger_entries_currency_length", "char_length(currency) = 3");
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

        builder.Property(x => x.Currency)
            .HasColumnName("currency")
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(x => x.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamp without time zone")
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
            .HasColumnType("timestamp without time zone")
            .IsRequired();

        builder.HasIndex(x => new { x.MerchantId, x.OccurredAt })
            .HasDatabaseName("idx_ledger_entries_merchant_occurred_at");
    }
}