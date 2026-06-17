using TransferService.Application;
using TransferService.Infrastructure;

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

        return services;
    }
}
