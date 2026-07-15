using ApiDefaults.Options;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace ApiDefaults.Tests.Support;

internal static class ServiceProviderTestFactory
{
    public static ServiceCollection CreateServices() => [];

    public static ServiceProvider BuildProvider(Action<IServiceCollection>? configure = null)
    {
        ServiceCollection services = CreateServices();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    public static IOptions<TOptions> Options<TOptions>(TOptions value)
        where TOptions : class
        => Microsoft.Extensions.Options.Options.Create(value);

    public static IOptions<ApiDefaultsOptions> ApiDefaultsOptions(long maxRequestBodySizeBytes)
        => Options(new ApiDefaultsOptions { MaxRequestBodySizeBytes = maxRequestBodySizeBytes });

    public static AuthenticationOptions AuthenticationOptions(Action<AuthenticationOptions>? configure = null)
    {
        var options = new AuthenticationOptions();
        configure?.Invoke(options);
        return options;
    }

    public static AuthorizationOptions AuthorizationOptions(Action<AuthorizationOptions>? configure = null)
    {
        var options = new AuthorizationOptions();
        configure?.Invoke(options);
        return options;
    }

    public static HealthCheckService HealthCheckService(Action<IServiceCollection>? configure = null)
        => BuildProvider(services =>
        {
            services.AddLogging();
            services.AddHealthChecks();
            configure?.Invoke(services);
        }).GetRequiredService<HealthCheckService>();

    public static HealthCheckOptions HealthCheckOptions(Action<HealthCheckOptions>? configure = null)
    {
        var options = new HealthCheckOptions();
        configure?.Invoke(options);
        return options;
    }

    public static SwaggerGenOptions OpenApiOptions(Action<SwaggerGenOptions>? configure = null)
    {
        var options = new SwaggerGenOptions();
        configure?.Invoke(options);
        return options;
    }
}
