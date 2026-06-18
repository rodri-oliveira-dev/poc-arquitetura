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
            .ValidateOnStart();

        services.AddHttpClient<ILedgerServiceClient, LedgerServiceClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TransferWorkerOptions>>().Value;
            if (options.Ledger.BaseAddress is not null)
                client.BaseAddress = options.Ledger.BaseAddress;
            client.Timeout = options.Ledger.Timeout;
        });

        services.AddSingleton<ITransferenciaKafkaProducer, KafkaTransferenciaOutboxPublisher>();
        services.AddHostedService<TransferenciaSagaProcessorService>();
        services.AddHostedService<TransferenciaOutboxPublisherService>();

        return services;
    }
}
