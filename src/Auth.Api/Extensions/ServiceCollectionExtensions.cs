using Auth.Api.Middlewares;
using Auth.Api.Observability;
using Auth.Api.Options;
using Auth.Api.Security;
using Auth.Api.Swagger;

using Microsoft.AspNetCore.HttpOverrides;

using OpenTelemetry.Metrics;
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
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddAuthApiHardening()
            .AddAuthApiOptions(configuration)
            .AddAuthApiSecurity()
            .AddAuthApiObservability(configuration)
            .AddApiSwagger();

        return services;
    }

    public static IServiceCollection AddAuthApiHardening(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto |
                ForwardedHeaders.XForwardedHost;
            options.ForwardLimit = 1;
            options.AllowedHosts.Add("auth.localhost");
            options.AllowedHosts.Add("localhost");

            // O IP do container Nginx e dinamico na rede bridge local.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }

    public static IServiceCollection AddAuthApiOptions(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.DevelopmentUser.Username), "Auth:DevelopmentUser:Username deve ser configurado.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DevelopmentUser.Password), "Auth:DevelopmentUser:Password deve ser configurado.")
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddAuthApiSecurity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IRsaKeyProvider, FileBackedRsaKeyProvider>();
        services.AddSingleton<IJwtIssuer, JwtIssuer>();

        return services;
    }

    /// <summary>
    /// Observabilidade mínima (OpenTelemetry opcional via config).
    /// </summary>
    public static IServiceCollection AddAuthApiObservability(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

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

        return services;
    }
}
