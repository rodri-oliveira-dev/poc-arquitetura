using LedgerService.Application;
using LedgerService.Infrastructure;

namespace LedgerService.Api.Extensions;

public static class ApiCompositionExtensions
{
    public static IServiceCollection AddLedgerApiComposition(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddApiHardening(configuration)
            .AddApiRateLimiting(configuration)
            .AddApiCors()
            .AddApiVersioningAndExplorer()
            .AddApiSwagger()
            .AddApiObservability(configuration);

        services.AddApiJwtAuth(configuration, environment);
        services.AddApplication();
        services
            .AddLedgerInfrastructureCommon()
            .AddLedgerPersistence(configuration)
            .AddLedgerRepositories();

        services.AddControllers();
        services.AddEndpointsApiExplorer();

        return services;
    }
}
