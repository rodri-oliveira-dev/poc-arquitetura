using ApiDefaults.Extensions;

using LedgerService.Api.Observability;
using LedgerService.Api.Security;
using LedgerService.Api.Swagger;
using LedgerService.Application.Common.Observability;

using Microsoft.OpenApi;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LedgerService.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        return services.AddApiSwaggerDefaults<ConfigureSwaggerOptions>(
            typeof(Program).Assembly,
            options =>
            {
                options.OperationFilter<LancamentosExamplesOperationFilter>();
                options.AddSecurityDefinition("Idempotency-Key", new OpenApiSecurityScheme
                {
                    Name = "Idempotency-Key",
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Description = "Chave de idempotencia (UUID). Requisicoes com a mesma chave e mesmo payload podem ser reprocessadas com replay da resposta. Se a mesma chave for usada com payload diferente, a API retorna 409."
                });
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
        services.AddOptions<OpenTelemetryOptions>()
            .Bind(configuration.GetSection(OpenTelemetryOptions.SectionName));

        OpenTelemetryOptions otelOptions = configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
            ?? new OpenTelemetryOptions();

        if (otelOptions.Enabled)
        {
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(otelOptions.ServiceName))
                .WithTracing(tracing =>
                {
                    tracing
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation();

                    if (otelOptions.UseConsoleExporter)
                    {
                        tracing.AddConsoleExporter();
                    }

                    if (!string.IsNullOrWhiteSpace(otelOptions.OtlpEndpoint))
                    {
                        tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otelOptions.OtlpEndpoint));
                    }
                })
                .WithMetrics(metrics =>
                {
                    metrics
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddMeter(LedgerDomainMetrics.MeterName);

                    if (otelOptions.UseConsoleExporter)
                    {
                        metrics.AddConsoleExporter();
                    }

                    if (!string.IsNullOrWhiteSpace(otelOptions.OtlpEndpoint))
                    {
                        metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otelOptions.OtlpEndpoint));
                    }
                });
        }

        return services;
    }
}
