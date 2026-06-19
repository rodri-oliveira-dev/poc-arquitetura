using ApiDefaults.Extensions;

using BalanceService.Api.Observability;
using BalanceService.Api.Security;
using BalanceService.Api.Swagger;
using BalanceService.Application.Common.Observability;

using Microsoft.OpenApi;

namespace BalanceService.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddApiSwaggerDefaults<ConfigureSwaggerOptions>(
            typeof(Program).Assembly,
            options =>
            {
                options.OperationFilter<ConsolidadosExamplesOperationFilter>();
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = $"Autenticacao via JWT Bearer. Obtenha um token no Keycloak local e informe: Bearer {{token}}.\n\nScopes relevantes nesta API: {ScopePolicies.BalanceRead} (leitura) / {ScopePolicies.BalanceWrite} (escrita - TODO se/when existirem endpoints de escrita)."
                });
                options.OperationFilter<AuthorizeOperationFilter>();
            });
    }

    public static IServiceCollection AddApiObservability(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services.AddConfiguredApiOpenTelemetryDefaults<OpenTelemetryOptions>(
            configuration,
            OpenTelemetryOptions.SectionName,
            options => options.Enabled,
            options => options.ServiceName,
            options => options.UseConsoleExporter,
            options => options.OtlpEndpoint,
            tracing => tracing
                .AddSource("BalanceService.Api")
                .AddSource("BalanceService.Application"),
            metrics => metrics.AddMeter(BalanceDomainMetrics.MeterName));
    }
}
