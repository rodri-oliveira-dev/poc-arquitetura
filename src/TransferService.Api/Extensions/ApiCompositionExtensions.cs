using TransferService.Application;
using TransferService.Infrastructure;

namespace TransferService.Api.Extensions;

public static class ApiCompositionExtensions
{
    public static IServiceCollection AddTransferApiComposition(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddTransferApplication();
        services.AddTransferInfrastructure(configuration, environment);

        return services;
    }
}
