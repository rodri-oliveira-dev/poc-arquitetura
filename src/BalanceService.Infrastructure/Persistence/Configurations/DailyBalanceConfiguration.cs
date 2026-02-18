using BalanceService.Domain.Balances;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BalanceService.Infrastructure.Persistence.Configurations;

public sealed class DailyBalanceConfiguration : IEntityTypeConfiguration<DailyBalance>
{
    public void Configure(EntityTypeBuilder<DailyBalance> builder)
    {
        builder.ToTable("daily_balances");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.MerchantId)
            .HasColumnName("merchant_id")
            .IsRequired();

        builder.Property(x => x.Date)
            .HasColumnName("date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasColumnName("currency")
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(x => x.TotalCredits)
            .HasColumnName("total_credits")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TotalDebits)
            .HasColumnName("total_debits")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.NetBalance)
            .HasColumnName("net_balance")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.AsOf)
            .HasColumnName("as_of")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(x => new { x.MerchantId, x.Date, x.Currency })
            .IsUnique()
            .HasDatabaseName("ux_daily_balances_merchant_date_currency");
    }
}
