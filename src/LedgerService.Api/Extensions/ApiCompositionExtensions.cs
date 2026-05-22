using LedgerService.Application;
using LedgerService.Infrastructure;
using LedgerService.Api.Contracts.Responses;

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

        services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = ValidationErrorResponseFactory.CreateResult;
            });
        services.AddEndpointsApiExplorer();

        return services;
    }
}
