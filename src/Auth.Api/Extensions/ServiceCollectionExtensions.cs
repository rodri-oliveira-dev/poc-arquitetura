using Auth.Api.Observability;
using Auth.Api.Options;
using Auth.Api.Security;
using Auth.Api.Swagger;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Auth.Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra todas as dependências do Auth.Api (options, segurança, observabilidade e swagger).
    /// </summary>
    public static IServiceCollection AddAuthApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddAuthApiOptions(configuration)
            .AddAuthApiSecurity()
            .AddAuthApiObservability(configuration)
            .AddApiSwagger();

        return services;
    }

    public static IServiceCollection AddAuthApiOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.DevelopmentUser.Username), "Auth:DevelopmentUser:Username deve ser configurado.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DevelopmentUser.Password), "Auth:DevelopmentUser:Password deve ser configurado.")
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddAuthApiSecurity(this IServiceCollection services)
    {
        services.AddSingleton<IRsaKeyProvider, FileBackedRsaKeyProvider>();
        services.AddSingleton<IJwtIssuer, JwtIssuer>();

        return services;
    }

    /// <summary>
    /// Observabilidade mínima (OpenTelemetry opcional via config).
    /// </summary>
    public static IServiceCollection AddAuthApiObservability(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenTelemetryOptions>()
            .Bind(configuration.GetSection(OpenTelemetryOptions.SectionName));

        var otelOptions = configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
            ?? new OpenTelemetryOptions();

        if (!otelOptions.Enabled)
            return services;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(otelOptions.ServiceName))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation();
                if (otelOptions.UseConsoleExporter)
                    tracing.AddConsoleExporter();
            });

        return services;
    }
}
