using IdentityService.Application.Idempotency.Ports;
using IdentityService.Application.Users.Ports;
using IdentityService.Infrastructure.DomainEvents;
using IdentityService.Infrastructure.Email;
using IdentityService.Infrastructure.IdentityProvider;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddIdentityDomainEvents()
            .AddIdentityPersistence(configuration)
            .AddIdentityRepositories()
            .AddIdentityProvider(configuration)
            .AddIdentityEmail(configuration);

        return services;
    }

    public static IServiceCollection AddIdentityPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' nao foi configurada.");

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));

        return services;
    }

    public static IServiceCollection AddIdentityRepositories(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMerchantIdGenerator, SequentialMerchantIdGenerator>();
        services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }

    public static IServiceCollection AddIdentityProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<KeycloakAdminOptions>(
            configuration.GetSection(KeycloakAdminOptions.SectionName));

        services.AddHttpClient<IIdentityProviderUserService, KeycloakAdminClient>((provider, httpClient) =>
        {
            var options = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<KeycloakAdminOptions>>()
                .Value;

            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
                httpClient.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);

            httpClient.Timeout = options.Timeout;
        });

        return services;
    }

    public static IServiceCollection AddIdentityEmail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<WelcomeEmailOptions>()
            .Bind(configuration.GetSection(WelcomeEmailOptions.SectionName));

        services.AddOptions<EmailProviderOptions>()
            .Bind(configuration.GetSection(EmailProviderOptions.SectionName));

        services.AddOptions<ResendOptions>()
            .Bind(configuration.GetSection(ResendOptions.SectionName));

        services.AddOptions<MailpitOptions>()
            .Bind(configuration.GetSection(MailpitOptions.SectionName));

        services.AddSingleton<IEmailTemplateRenderer, FileEmailTemplateRenderer>();
        services.AddHttpClient<IResendClientFactory, ResendClientFactory>();
        services.AddScoped<IEmailSender>(provider =>
        {
            var providerOptions = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailProviderOptions>>()
                .Value;

            return providerOptions.Provider switch
            {
                var value when string.Equals(value, EmailProviderOptions.Resend, StringComparison.OrdinalIgnoreCase) =>
                    provider.GetRequiredService<ResendEmailSender>(),
                var value when string.Equals(value, EmailProviderOptions.Mailpit, StringComparison.OrdinalIgnoreCase) =>
                    provider.GetRequiredService<MailpitEmailSender>(),
                _ => throw new InvalidOperationException(
                    $"Email:Provider '{providerOptions.Provider}' nao e suportado. Valores aceitos: Resend, Mailpit.")
            };
        });
        services.AddScoped<ResendEmailSender>();
        services.AddScoped<MailpitEmailSender>();

        return services;
    }
}
