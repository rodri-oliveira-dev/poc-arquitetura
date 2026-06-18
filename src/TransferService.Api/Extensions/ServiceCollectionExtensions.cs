using ApiDefaults.Extensions;

using Microsoft.OpenApi;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using TransferService.Api.Observability;
using TransferService.Api.Security;
using TransferService.Api.Swagger;

namespace TransferService.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddApiSwaggerDefaults<ConfigureSwaggerOptions>(
            typeof(Program).Assembly,
            options =>
            {
                options.AddSecurityDefinition("Idempotency-Key", new OpenApiSecurityScheme
                {
                    Name = "Idempotency-Key",
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Description = "Chave de idempotencia (UUID). Requisicoes com a mesma chave e mesmo payload retornam replay da resposta. Se a mesma chave for usada com payload diferente, a API retorna 409."
                });
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = $"Autenticacao via JWT Bearer. Scopes relevantes nesta API: {ScopePolicies.TransferWrite} (escrita) / {ScopePolicies.TransferRead} (leitura)."
                });
                options.OperationFilter<AuthorizeOperationFilter>();
            });
    }

    public static IServiceCollection AddApiObservability(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<OpenTelemetryOptions>()
            .Bind(configuration.GetSection(OpenTelemetryOptions.SectionName));

        OpenTelemetryOptions otelOptions = configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
            ?? new OpenTelemetryOptions();

        if (!otelOptions.Enabled)
        {
            return services;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(otelOptions.ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("TransferService.Api")
                    .AddSource("TransferService.Application");

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
                    .AddRuntimeInstrumentation();

                if (otelOptions.UseConsoleExporter)
                {
                    metrics.AddConsoleExporter();
                }

                if (!string.IsNullOrWhiteSpace(otelOptions.OtlpEndpoint))
                {
                    metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otelOptions.OtlpEndpoint));
                }
            });

        return services;
    }
}
