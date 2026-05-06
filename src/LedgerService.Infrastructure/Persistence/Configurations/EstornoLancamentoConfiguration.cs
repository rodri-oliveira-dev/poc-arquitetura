using LedgerService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerService.Infrastructure.Persistence.Configurations;

public sealed class EstornoLancamentoConfiguration : IEntityTypeConfiguration<EstornoLancamento>
{
    public void Configure(EntityTypeBuilder<EstornoLancamento> builder)
    {
        builder.ToTable("estornos_lancamentos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.LancamentoOriginalId)
            .HasColumnName("lancamento_original_id")
            .IsRequired();

        builder.Property(x => x.MerchantId)
            .HasColumnName("merchant_id")
            .IsRequired();

        builder.Property(x => x.Motivo)
            .HasColumnName("motivo")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp without time zone")
            .IsRequired();

        builder.HasIndex(x => x.LancamentoOriginalId)
            .HasDatabaseName("idx_estornos_lancamentos_original");

        builder.HasIndex(x => new { x.LancamentoOriginalId, x.Status })
            .HasDatabaseName("idx_estornos_lancamentos_original_status");
    }
}
