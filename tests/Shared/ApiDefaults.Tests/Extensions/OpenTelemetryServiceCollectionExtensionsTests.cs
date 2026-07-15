using ApiDefaults.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ApiDefaults.Tests.Extensions;

public sealed class OpenTelemetryServiceCollectionExtensionsTests
{
    [Fact]
    public void AddConfiguredApiOpenTelemetryDefaults_WhenDisabled_ShouldBindOptionsWithoutRegisteringHostedTelemetry()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["OpenTelemetry:Enabled"] = "false",
            ["OpenTelemetry:ServiceName"] = "disabled-api"
        });

        services.AddConfiguredApiOpenTelemetryDefaults<TestOpenTelemetryOptions>(
            configuration,
            "OpenTelemetry",
            options => options.Enabled,
            options => options.ServiceName,
            options => options.UseConsoleExporter,
            options => options.OtlpEndpoint);

        using ServiceProvider provider = BuildValidatedProvider(services);

        Assert.False(provider.GetRequiredService<IOptions<TestOpenTelemetryOptions>>().Value.Enabled);
        Assert.DoesNotContain(provider.GetServices<IHostedService>(), service =>
            service.GetType().FullName?.Contains("OpenTelemetry", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void AddConfiguredApiOpenTelemetryDefaults_WhenEnabled_ShouldInvokeTracingAndMetricsCustomization()
    {
        var services = new ServiceCollection();
        bool tracingConfigured = false;
        bool metricsConfigured = false;
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["OpenTelemetry:Enabled"] = "true",
            ["OpenTelemetry:ServiceName"] = "shared-api",
            ["OpenTelemetry:UseConsoleExporter"] = "false"
        });

        services.AddConfiguredApiOpenTelemetryDefaults<TestOpenTelemetryOptions>(
            configuration,
            "OpenTelemetry",
            options => options.Enabled,
            options => options.ServiceName,
            options => options.UseConsoleExporter,
            options => options.OtlpEndpoint,
            tracing =>
            {
                tracingConfigured = true;
                tracing.AddSource("tests.shared.api");
            },
            metrics =>
            {
                metricsConfigured = true;
                metrics.AddMeter("tests.shared.api");
            });

        using ServiceProvider provider = BuildValidatedProvider(services);

        Assert.True(provider.GetRequiredService<IOptions<TestOpenTelemetryOptions>>().Value.Enabled);
        Assert.Contains(provider.GetServices<IHostedService>(), service =>
            service.GetType().FullName?.Contains("OpenTelemetry", StringComparison.Ordinal) == true);
        Assert.True(tracingConfigured);
        Assert.True(metricsConfigured);
    }

    [Fact]
    public void AddApiOpenTelemetryDefaults_WhenServiceNameIsEmpty_ShouldThrow()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>("serviceName", () =>
            services.AddApiOpenTelemetryDefaults(" ", useConsoleExporter: false, otlpEndpoint: null));
    }

    [Fact]
    public void AddApiOpenTelemetryDefaults_WhenOtlpEndpointIsInvalid_ShouldRegisterWithoutNetworkCall()
    {
        var services = new ServiceCollection();

        services.AddApiOpenTelemetryDefaults("shared-api", useConsoleExporter: false, otlpEndpoint: "http://[::1");

        using ServiceProvider provider = BuildValidatedProvider(services);

        Assert.Contains(provider.GetServices<IHostedService>(), service =>
            service.GetType().FullName?.Contains("OpenTelemetry", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void AddApiOpenTelemetryDefaults_WhenCalledTwice_ShouldBuildProvider()
    {
        var services = new ServiceCollection();

        services.AddApiOpenTelemetryDefaults("shared-api", useConsoleExporter: false, otlpEndpoint: null);
        services.AddApiOpenTelemetryDefaults("shared-api", useConsoleExporter: true, otlpEndpoint: null);

        using ServiceProvider provider = BuildValidatedProvider(services);

        Assert.Contains(provider.GetServices<IHostedService>(), service =>
            service.GetType().FullName?.Contains("OpenTelemetry", StringComparison.Ordinal) == true);
    }

    private static ServiceProvider BuildValidatedProvider(IServiceCollection services)
        => services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private sealed class TestOpenTelemetryOptions
    {
        public bool Enabled
        {
            get; init;
        }

        public string ServiceName { get; init; } = string.Empty;

        public bool UseConsoleExporter
        {
            get; init;
        }

        public string? OtlpEndpoint
        {
            get; init;
        }
    }
}
