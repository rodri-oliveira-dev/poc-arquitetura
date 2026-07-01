using ApiDefaults.Extensions;

using TransferService.Application;
using TransferService.Infrastructure;
using TransferService.Api.Contracts.Responses;
using TransferService.Api.Middlewares;

namespace TransferService.Api.Extensions;

public static class ApiCompositionExtensions
{
    public static IServiceCollection AddTransferApiComposition(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddApiDefaults<GlobalExceptionHandler>(configuration, "transfer.localhost", "localhost")
            .AddApiSwagger()
            .AddApiObservability(configuration);

        services.AddApiJwtAuth(configuration, environment);
        services.AddTransferApplication();
        services.AddTransferInfrastructure(configuration, environment);

        services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = ValidationErrorResponseFactory.CreateResult;
            });
        services.AddEndpointsApiExplorer();

        return services;
    }
}
