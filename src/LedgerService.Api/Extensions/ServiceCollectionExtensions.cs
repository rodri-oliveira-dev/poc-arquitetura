using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using LedgerService.Api.Middlewares;
using LedgerService.Api.Observability;
using LedgerService.Api.Swagger;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Threading.RateLimiting;

namespace LedgerService.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiHardening(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("fixed", config =>
            {
                config.PermitLimit = 100;
                config.Window = TimeSpan.FromMinutes(1);
                config.QueueLimit = 10;
                config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
        });

        return services;
    }

    public static IServiceCollection AddApiCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("ApiCorsPolicy", policy =>
            {
                policy
                    .WithOrigins(
                        "http://localhost:3000",
                        "http://localhost:5173",
                        "https://localhost:3001",
                        "https://localhost:5173")
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                    .WithHeaders("Content-Type", "Authorization")
                    .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
            });
        });

        return services;
    }

    /// <summary>
    /// Configura versionamento da API e ApiExplorer para Swagger versionado.
    /// Estratégia: URL segment (ex.: /api/v1/...)
    /// </summary>
    public static IServiceCollection AddApiVersioningAndExplorer(this IServiceCollection services)
    {
        var versioningBuilder = services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc();

        versioningBuilder.AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }

    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        // Swagger por versão depende do IApiVersionDescriptionProvider
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

        services.AddSwaggerGen(options =>
        {
            // Inclui endpoints apenas no documento da respectiva versão.
            options.DocInclusionPredicate((docName, apiDesc) =>
                string.Equals(apiDesc.GroupName, docName, StringComparison.OrdinalIgnoreCase));

            // XML comments para enriquecer Swagger/OpenAPI com summary/remarks de controllers e DTOs.
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // Headers comuns da API (correlação e idempotência)
            options.AddSecurityDefinition("Idempotency-Key", new OpenApiSecurityScheme
            {
                Name = "Idempotency-Key",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Chave de idempotência (UUID). Requisições com a mesma chave e mesmo payload podem ser reprocessadas com replay da resposta. Se a mesma chave for usada com payload diferente, a API retorna 409."
            });

            options.AddSecurityDefinition(CorrelationIdMiddleware.HeaderName, new OpenApiSecurityScheme
            {
                Name = CorrelationIdMiddleware.HeaderName,
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Identificador de correlação (UUID). Se não for enviado, a API gera um novo e o retorna no header de response."
            });
        });

        return services;
    }

    public static IServiceCollection AddApiObservability(this IServiceCollection services, IConfiguration configuration)
    {
        // Observabilidade (OpenTelemetry)
        // - Por padrão fica desabilitado.
        // - Pode ser habilitado via config: Observability:OpenTelemetry:Enabled=true
        // - Para validar localmente sem backend, use Observability:OpenTelemetry:UseConsoleExporter=true
        services.AddOptions<OpenTelemetryOptions>()
            .Bind(configuration.GetSection(OpenTelemetryOptions.SectionName));

        var otelOptions = configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
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
                        tracing.AddConsoleExporter();
                })
                .WithMetrics(metrics =>
                {
                    metrics
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation();

                    if (otelOptions.UseConsoleExporter)
                        metrics.AddConsoleExporter();
                });
        }

        return services;
    }
}
