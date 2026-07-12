using HttpResilienceDefaults;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using PaymentService.Application.Abstractions.Gateway;
using PaymentService.Application.Abstractions.Ledger;
using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Infrastructure.Gateway;
using PaymentService.Infrastructure.Ledger;
using PaymentService.Infrastructure.Persistence;
using PaymentService.Infrastructure.Persistence.Repositories;

namespace PaymentService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services
            .AddPaymentPersistence(configuration)
            .AddPaymentRepositories()
            .AddPaymentGateway(configuration, environment)
            .AddPaymentLedgerGateway(configuration);

        return services;
    }

    public static IServiceCollection AddPaymentPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' nao foi configurada.");

        services.AddDbContext<PaymentDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "payment")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PaymentDbContext>());

        return services;
    }

    public static IServiceCollection AddPaymentRepositories(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentIdempotencyService, PaymentIdempotencyService>();
        services.AddScoped<IPaymentInboxRepository, PaymentInboxRepository>();

        return services;
    }

    public static IServiceCollection AddPaymentGateway(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddOptions<PaymentGatewayOptions>()
            .Bind(configuration.GetSection(PaymentGatewayOptions.SectionName))
            .Validate(options =>
                string.Equals(options.Provider, PaymentGatewayProviders.Fake, StringComparison.OrdinalIgnoreCase)
                || string.Equals(options.Provider, PaymentGatewayProviders.Stripe, StringComparison.OrdinalIgnoreCase),
                "PaymentGateway:Provider deve ser Fake ou Stripe.")
            .Validate(options =>
                !string.Equals(options.Provider, PaymentGatewayProviders.Stripe, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(options.Stripe.EffectiveSecretKey),
                "PaymentGateway:Stripe:SecretKey deve ser configurada quando PaymentGateway:Provider=Stripe.")
            .Validate(options =>
                !string.Equals(options.Provider, PaymentGatewayProviders.Stripe, StringComparison.OrdinalIgnoreCase)
                || options.Stripe.Timeout > TimeSpan.Zero,
                "PaymentGateway:Stripe:Timeout deve ser maior que zero.")
            .Validate(options =>
                options.Stripe.WebhookSignatureTolerance > TimeSpan.Zero,
                "PaymentGateway:Stripe:WebhookSignatureTolerance deve ser maior que zero.")
            .ValidateOnStart();

        services.AddSingleton<PaymentGatewayTelemetry>();
        services.AddScoped<FakePaymentGateway>();
        services.AddHttpClient<StripePaymentGateway>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<PaymentGatewayOptions>>().Value.Stripe;
            client.BaseAddress = options.ApiBaseUrl;
            client.Timeout = options.Timeout;
        })
            .AddConfiguredHttpResilience(configuration, "Stripe");

        services.AddScoped<IPaymentGateway>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PaymentGatewayOptions>>().Value;
            return string.Equals(options.Provider, PaymentGatewayProviders.Stripe, StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<StripePaymentGateway>()
                : sp.GetRequiredService<FakePaymentGateway>();
        });

        return services;
    }

    public static IServiceCollection AddPaymentLedgerGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<PaymentLedgerOptions>()
            .Bind(configuration.GetSection(PaymentLedgerOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient<ILedgerAccessTokenProvider, ClientCredentialsLedgerAccessTokenProvider>()
            .AddConfiguredHttpResilience(configuration, "Keycloak");
        services.AddTransient<LedgerAuthenticationHandler>();
        services.AddHttpClient<ILedgerEntryGateway, LedgerHttpGateway>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<PaymentLedgerOptions>>().Value;
            if (options.BaseAddress is not null)
                client.BaseAddress = options.BaseAddress;

            client.Timeout = options.Timeout;
        })
            .AddConfiguredHttpResilience(configuration, "Ledger")
            .AddHttpMessageHandler<LedgerAuthenticationHandler>();

        return services;
    }
}
