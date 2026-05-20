using LedgerService.Application;
using LedgerService.Infrastructure;

namespace LedgerService.Worker.Extensions;

public static class WorkerCompositionExtensions
{
    public static IServiceCollection AddLedgerWorkerComposition(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddWorkerObservability(configuration);
        services.AddApplication();
        services
            .AddLedgerInfrastructureCommon()
            .AddLedgerPersistence(configuration)
            .AddLedgerRepositories()
            .AddLedgerKafkaProducer(configuration, environment)
            .AddLedgerOutboxWorker(configuration)
            .AddLedgerEstornoWorker(configuration)
            .AddLedgerReprocessamentoWorker(configuration, environment);

        return services;
    }
}
