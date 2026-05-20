using BalanceService.Application;
using BalanceService.Infrastructure;

namespace BalanceService.Worker.Extensions;

public static class WorkerCompositionExtensions
{
    public static IServiceCollection AddBalanceWorkerComposition(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddWorkerObservability(configuration);
        services.AddApplication();
        services
            .AddBalanceInfrastructureCommon()
            .AddBalancePersistence(configuration)
            .AddBalanceRepositories()
            .AddBalanceKafkaConsumer(configuration, environment)
            .AddBalanceLedgerEventsWorker(configuration);

        return services;
    }
}
