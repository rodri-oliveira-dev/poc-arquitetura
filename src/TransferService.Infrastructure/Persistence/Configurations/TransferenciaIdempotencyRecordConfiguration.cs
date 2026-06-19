using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TransferService.Infrastructure.Persistence.Configurations;

public sealed class TransferenciaIdempotencyRecordConfiguration : IEntityTypeConfiguration<TransferenciaIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<TransferenciaIdempotencyRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("idempotency_records");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.SourceMerchantId)
            .HasColumnName("source_merchant_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.RequestHash)
            .HasColumnName("request_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.TransferenciaId)
            .HasColumnName("transferencia_id")
            .IsRequired();

        builder.Property(x => x.ResponseBody)
            .HasColumnName("response_body")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.HasIndex(x => new { x.SourceMerchantId, x.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ux_transfer_idempotency_source_key");

        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("idx_transfer_idempotency_expires_at");
    }
}
