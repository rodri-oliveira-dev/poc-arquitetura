using HttpResilienceDefaults;

using TransferService.Application;
using TransferService.Infrastructure;
using TransferService.Worker.Ledger;
using TransferService.Worker.Messaging;
using TransferService.Worker.Options;
using TransferService.Worker.Outbox;
using TransferService.Worker.Sagas;

namespace TransferService.Worker.Extensions;

public static class WorkerCompositionExtensions
{
    public static IServiceCollection AddTransferWorkerComposition(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddTransferApplication();
        services.AddTransferInfrastructure(configuration, environment);
        services.AddTransferWorkerRuntime(configuration);

        return services;
    }

    private static IServiceCollection AddTransferWorkerRuntime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<TransferWorkerOptions>()
            .Bind(configuration.GetSection(TransferWorkerOptions.SectionName))
            .Validate(o => o.PollingInterval > TimeSpan.Zero, "TransferService Worker PollingInterval deve ser maior que zero.")
            .Validate(o => o.BatchSize > 0, "TransferService Worker BatchSize deve ser maior que zero.")
            .Validate(o => o.MaxRetryCount > 0, "TransferService Worker MaxRetryCount deve ser maior que zero.")
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.Kafka.BootstrapServers), "TransferService Worker Kafka BootstrapServers nao configurado.")
            .Validate(o => !o.Enabled || o.Ledger.BaseAddress is not null, "TransferService Worker Ledger BaseAddress nao configurado.")
            .Validate(o => !o.Enabled || o.Ledger.Auth.TokenEndpoint is not null, "TransferService Worker Ledger Auth TokenEndpoint nao configurado.")
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.Ledger.Auth.ClientId), "TransferService Worker Ledger Auth ClientId nao configurado.")
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.Ledger.Auth.ClientSecret), "TransferService Worker Ledger Auth ClientSecret nao configurado.")
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.Ledger.Auth.Scope), "TransferService Worker Ledger Auth Scope nao configurado.")
            .Validate(o => !o.Enabled || o.Ledger.Auth.RefreshSkew > TimeSpan.Zero, "TransferService Worker Ledger Auth RefreshSkew deve ser maior que zero.")
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient<ILedgerAccessTokenProvider, ClientCredentialsLedgerAccessTokenProvider>()
            .AddConfiguredHttpResilience(configuration, "Keycloak");
        services.AddTransient<LedgerAuthenticationHandler>();
        services.AddHttpClient<ILedgerServiceClient, LedgerServiceClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TransferWorkerOptions>>().Value;
            if (options.Ledger.BaseAddress is not null)
            {
                client.BaseAddress = options.Ledger.BaseAddress;
            }

            client.Timeout = options.Ledger.Timeout;
        })
            .AddConfiguredHttpResilience(configuration, "Ledger")
            .AddHttpMessageHandler<LedgerAuthenticationHandler>();

        services.AddSingleton<ITransferenciaKafkaProducer, KafkaTransferenciaOutboxPublisher>();
        services.AddHostedService<TransferenciaSagaProcessorService>();
        services.AddHostedService<TransferenciaOutboxPublisherService>();

        return services;
    }
}
