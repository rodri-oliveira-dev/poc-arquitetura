using BalanceService.Application;
using BalanceService.Infrastructure;
using BalanceService.Api.Contracts;

namespace BalanceService.Api.Extensions;

public static class ApiCompositionExtensions
{
    public static IServiceCollection AddBalanceApiComposition(
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
            .AddBalanceInfrastructureCommon()
            .AddBalancePersistence(configuration)
            .AddBalanceRepositories();

        services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = ValidationErrorResponseFactory.CreateResult;
            });
        services.AddEndpointsApiExplorer();

        return services;
    }
}
