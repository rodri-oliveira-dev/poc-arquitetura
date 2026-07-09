using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PaymentService.Infrastructure.Persistence.Configurations;

public sealed class PaymentIdempotencyRecordConfiguration : IEntityTypeConfiguration<PaymentIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<PaymentIdempotencyRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("idempotency_records");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.MerchantId)
            .HasColumnName("merchant_id")
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

        builder.Property(x => x.ResponseBody)
            .HasColumnName("response_body")
            .HasColumnType(PostgreSqlColumnTypes.Jsonb)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.HasIndex(x => new { x.MerchantId, x.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ux_payment_idempotency_merchant_key");

        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("idx_payment_idempotency_expires_at");
    }
}
