using ApiDefaults.Authentication;
using ApiDefaults.Extensions;

using AuditService.Api.Observability;
using AuditService.Api.Security;
using AuditService.Api.Swagger;

using Microsoft.OpenApi;

namespace AuditService.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddApiSwaggerDefaults<ConfigureSwaggerOptions>(
            typeof(Program).Assembly,
            static options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = $"Autenticacao via JWT Bearer. Audience esperada: audit-api. Scopes: {AuditScopePolicies.AuditWrite}, {AuditScopePolicies.AuditRead}, {AuditScopePolicies.AuditAdmin}."
                });
                options.OperationFilter<AuthorizeOperationFilter>();
                options.DocumentFilter<AuditOpenApiDocumentFilter>();
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
                .AddSource("AuditService.Api")
                .AddSource("AuditService.Application"));
    }

    public static IServiceCollection AddAuditApiSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        return services.AddApiJwtBearerAuthentication(
            ReadJwtOptions(configuration, "Jwt"),
            environment,
            static options => options.AddAuditScopePolicies(),
            configuration);
    }

    private static ApiJwtAuthenticationOptions ReadJwtOptions(IConfiguration configuration, string sectionName)
        => new(
            sectionName,
            configuration.GetValue<string>($"{sectionName}:Issuer") ?? string.Empty,
            configuration.GetValue<string>($"{sectionName}:Audience") ?? string.Empty,
            configuration.GetValue<string>($"{sectionName}:JwksUrl") ?? string.Empty,
            configuration.GetValue($"{sectionName}:RequireHttpsMetadata", true),
            configuration.GetValue($"{sectionName}:JwksTimeoutSeconds", 5),
            configuration.GetValue($"{sectionName}:JwksRetryCount", 2),
            configuration.GetValue($"{sectionName}:JwksRetryBaseDelayMilliseconds", 200));
}
