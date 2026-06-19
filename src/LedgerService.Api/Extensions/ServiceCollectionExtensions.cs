using ApiDefaults.Extensions;

using LedgerService.Api.Observability;
using LedgerService.Api.Security;
using LedgerService.Api.Swagger;
using LedgerService.Application.Common.Observability;

using Microsoft.OpenApi;

namespace LedgerService.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddApiSwaggerDefaults<ConfigureSwaggerOptions>(
            typeof(Program).Assembly,
            options =>
            {
                options.OperationFilter<LancamentosExamplesOperationFilter>();
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = $"Autenticacao via JWT Bearer. Obtenha um token no Keycloak local e informe: Bearer {{token}}.\n\nScopes relevantes nesta API: {ScopePolicies.LedgerWrite} (escrita) / {ScopePolicies.LedgerRead} (leitura) / {ScopePolicies.OutboxAdmin} (administracao da DLQ do Outbox)."
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
            configureMetrics: metrics => metrics.AddMeter(LedgerDomainMetrics.MeterName));
    }
}
