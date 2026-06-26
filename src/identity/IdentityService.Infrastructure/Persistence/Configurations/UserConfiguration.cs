using IdentityService.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Ignore(x => x.DomainEvents);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new UserId(value))
            .ValueGeneratedNever();

        builder.Property(x => x.KeycloakUserId)
            .HasColumnName("keycloak_user_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.MerchantId)
            .HasColumnName("merchant_id")
            .HasConversion(id => id.Value, value => new MerchantId(value))
            .HasMaxLength(MerchantId.MaxLength)
            .IsRequired();

        builder.Property(x => x.Username)
            .HasColumnName("username")
            .HasConversion(username => username.Value, value => new Username(value))
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasConversion(email => email.Value, value => new Email(value))
            .HasMaxLength(320)
            .IsRequired();

        builder.Property<string?>("Document")
            .HasColumnName("document")
            .HasMaxLength(50);

        builder.Property<bool>("IsActive")
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property<DateTime>("CreatedAtUtc")
            .HasColumnName("created_at_utc")
            .HasColumnType(PostgreSqlColumnTypes.TimestampWithTimeZone)
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd()
            .IsRequired();

        builder.HasIndex(x => x.Email)
            .HasDatabaseName("ux_identity_users_email")
            .IsUnique();

        builder.HasIndex(x => x.KeycloakUserId)
            .HasDatabaseName("ux_identity_users_keycloak_user_id")
            .IsUnique();

        builder.HasIndex(x => x.MerchantId)
            .HasDatabaseName("idx_identity_users_merchant_id");
    }
}
