using ApiDefaults.Extensions;

using AuditService.Api.Middlewares;
using AuditService.Application;
using AuditService.Infrastructure;

namespace AuditService.Api.Extensions;

public static class ApiCompositionExtensions
{
    public static IServiceCollection AddAuditApiComposition(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services
            .AddApiDefaults<GlobalExceptionHandler>(configuration, "audit.localhost", "localhost")
            .AddApiSwagger()
            .AddAuditApiSecurity(configuration, environment)
            .AddApiObservability(configuration);

        services.AddAuditApplication();
        services.AddAuditInfrastructure(configuration, environment);

        services.AddControllers();
        services.AddEndpointsApiExplorer();

        return services;
    }
}
