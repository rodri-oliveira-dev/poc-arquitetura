using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LedgerService.Domain.Entities;

namespace LedgerService.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.MerchantId)
            .HasColumnName("merchant_id")
            .IsRequired();

        builder.Property(x => x.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .IsRequired();

        builder.Property(x => x.RequestHash)
            .HasColumnName("request_hash")
            .IsRequired();

        builder.Property(x => x.LedgerEntryId)
            .HasColumnName("ledger_entry_id");

        builder.Property(x => x.ResponseStatusCode)
            .HasColumnName("response_status_code")
            .IsRequired();

        builder.Property(x => x.ResponseBody)
            .HasColumnName("response_body")
            .HasColumnType("jsonb");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp without time zone")
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp without time zone")
            .IsRequired();

        builder.HasIndex(x => new { x.MerchantId, x.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ux_idempotency_records_merchant_key");

        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("idx_idempotency_records_expires_at");
    }
}