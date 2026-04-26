using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using LedgerService.Api.Middlewares;
using LedgerService.Api.Observability;
using LedgerService.Api.Options;
using LedgerService.Api.Security;
using LedgerService.Api.Swagger;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Threading.RateLimiting;

namespace LedgerService.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiHardening(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        services
            .AddOptions<ApiLimitsOptions>()
            .Bind(configuration.GetSection(ApiLimitsOptions.SectionName))
            .Validate(options => options.MaxRequestBodySizeBytes > 0, "ApiLimits:MaxRequestBodySizeBytes must be greater than zero.")
            .Validate(options => options.RateLimitPermitLimit > 0, "ApiLimits:RateLimitPermitLimit must be greater than zero.")
            .Validate(options => options.RateLimitWindowSeconds > 0, "ApiLimits:RateLimitWindowSeconds must be greater than zero.")
            .Validate(options => options.RateLimitQueueLimit >= 0, "ApiLimits:RateLimitQueueLimit must be zero or greater.")
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var apiLimits = configuration.GetSection(ApiLimitsOptions.SectionName).Get<ApiLimitsOptions>()
            ?? new ApiLimitsOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("fixed", config =>
            {
                config.PermitLimit = apiLimits.RateLimitPermitLimit;
                config.Window = TimeSpan.FromSeconds(apiLimits.RateLimitWindowSeconds);
                config.QueueLimit = apiLimits.RateLimitQueueLimit;
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
                    .WithHeaders("Content-Type", "Authorization", "Idempotency-Key", CorrelationIdMiddleware.HeaderName)
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
            options.EnableAnnotations();
            // Inclui endpoints apenas no documento da respectiva versão.
            options.DocInclusionPredicate((docName, apiDesc) =>
                string.Equals(apiDesc.GroupName, docName, StringComparison.OrdinalIgnoreCase));

            // XML comments para enriquecer Swagger/OpenAPI com summary/remarks de controllers e DTOs.
            var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: false);
            }

            options.OperationFilter<LancamentosExamplesOperationFilter>();

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

            // JWT Bearer
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = $"Autenticação via JWT Bearer. Obtenha um token no Auth.Api (POST /auth/login) e informe: Bearer {{token}}.\n\nScopes relevantes nesta API: {ScopePolicies.LedgerWrite} (escrita) / {ScopePolicies.LedgerRead} (leitura - TODO se/when existirem endpoints de leitura)."
            });

            // Aplica requirement + descrição de scopes por endpoint com [Authorize]
            options.OperationFilter<AuthorizeOperationFilter>();
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

                    if (!string.IsNullOrWhiteSpace(otelOptions.OtlpEndpoint))
                        tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otelOptions.OtlpEndpoint));
                })
                .WithMetrics(metrics =>
                {
                    metrics
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation();

                    if (otelOptions.UseConsoleExporter)
                        metrics.AddConsoleExporter();

                    if (!string.IsNullOrWhiteSpace(otelOptions.OtlpEndpoint))
                        metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otelOptions.OtlpEndpoint));
                });
        }

        return services;
    }
}
