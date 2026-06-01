using ApiDefaults.Extensions;

using BalanceService.Api.Middlewares;
using BalanceService.Api.Options;
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
            .AddApiDefaults<GlobalExceptionHandler>(configuration, "balance.localhost", "localhost")
            .AddApiSwagger()
            .AddApiObservability(configuration);

        services.AddApiJwtAuth(configuration, environment);
        services
            .AddOptions<ApiLimitsOptions>()
            .Bind(configuration.GetSection(ApiLimitsOptions.SectionName))
            .Validate(options => options.MaxBalancePeriodDays > 0, "ApiLimits:MaxBalancePeriodDays must be greater than zero.")
            .ValidateOnStart();
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
