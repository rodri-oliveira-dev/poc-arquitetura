using LedgerService.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerService.Infrastructure.Persistence.Configurations;

public sealed class ReprocessamentoLancamentosConfiguration
    : IEntityTypeConfiguration<ReprocessamentoLancamentos>
{
    public void Configure(EntityTypeBuilder<ReprocessamentoLancamentos> builder)
    {
        builder.ToTable("reprocessamentos_lancamentos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.MerchantId)
            .HasColumnName("merchant_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DataInicial)
            .HasColumnName("data_inicial")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.DataFinal)
            .HasColumnName("data_final")
            .HasColumnType("date")
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
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .IsRequired();

        builder.Property(x => x.ProcessingStartedAt)
            .HasColumnName("processing_started_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.FailedAt)
            .HasColumnName("failed_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.RejectedAt)
            .HasColumnName("rejected_at")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone);

        builder.Property(x => x.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(500);

        builder.Property(x => x.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.MerchantId, x.DataInicial, x.DataFinal })
            .HasDatabaseName("idx_reprocessamentos_lancamentos_merchant_periodo");
    }
}
