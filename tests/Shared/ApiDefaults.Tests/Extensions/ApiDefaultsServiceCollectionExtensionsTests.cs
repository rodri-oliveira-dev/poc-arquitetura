using System.Net;

using ApiDefaults.Extensions;
using ApiDefaults.Options;

using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace ApiDefaults.Tests.Extensions;

public sealed class ApiDefaultsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddApiDefaults_WhenServicesIsNull_ShouldThrow()
    {
        IConfiguration configuration = CreateConfiguration();

        Assert.Throws<ArgumentNullException>("services", () =>
            ApiDefaultsServiceCollectionExtensions.AddApiDefaults<TestExceptionHandler>(
                null!,
                configuration));
    }

    [Fact]
    public void AddApiDefaults_WhenConfigurationIsNull_ShouldThrow()
    {
        IServiceCollection services = CreateHostServices();

        Assert.Throws<ArgumentNullException>("configuration", () =>
            services.AddApiDefaults<TestExceptionHandler>(null!));
    }

    [Fact]
    public void AddApiDefaults_ShouldRegisterCriticalServicesAndBuildValidatedProvider()
    {
        IServiceCollection services = CreateHostServices();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ApiLimits:MaxRequestBodySizeBytes"] = "2048",
            ["ApiLimits:RateLimitPermitLimit"] = "7",
            ["ApiLimits:RateLimitWindowSeconds"] = "11",
            ["ApiLimits:RateLimitQueueLimit"] = "3",
            ["ForwardedHeaders:AllowedHosts:0"] = "api.localhost",
            ["ForwardedHeaders:AllowedHosts:1"] = "localhost"
        });

        services.AddApiDefaults<TestExceptionHandler>(configuration);

        using ServiceProvider provider = BuildValidatedProvider(services);

        ApiDefaultsOptions apiOptions = provider.GetRequiredService<IOptions<ApiDefaultsOptions>>().Value;
        Assert.Equal(2048, apiOptions.MaxRequestBodySizeBytes);
        Assert.Equal(7, apiOptions.RateLimitPermitLimit);
        Assert.Equal(11, apiOptions.RateLimitWindowSeconds);
        Assert.Equal(3, apiOptions.RateLimitQueueLimit);
        Assert.NotNull(provider.GetRequiredService<IProblemDetailsService>());
        Assert.NotNull(provider.GetRequiredService<IOptions<RateLimiterOptions>>());
        Assert.NotNull(provider.GetRequiredService<IApiVersionDescriptionProvider>());
        Assert.NotNull(provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>());
    }

    [Fact]
    public void AddApiDefaults_WhenCalledTwice_ShouldKeepOptionsResolvableAndForwardedHostsConfigured()
    {
        IServiceCollection services = CreateHostServices();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:AllowedHosts:0"] = "api.localhost",
            ["ForwardedHeaders:AllowedHosts:1"] = "internal.localhost"
        });

        services.AddApiDefaults<TestExceptionHandler>(configuration);
        services.AddApiDefaults<TestExceptionHandler>(configuration);

        using ServiceProvider provider = BuildValidatedProvider(services);

        ForwardedHeadersOptions forwarded = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.Contains("api.localhost", forwarded.AllowedHosts);
        Assert.Contains("internal.localhost", forwarded.AllowedHosts);
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost, forwarded.ForwardedHeaders);
        Assert.Equal(1, forwarded.ForwardLimit);
        Assert.Empty(forwarded.KnownIPNetworks);
        Assert.Empty(forwarded.KnownProxies);
    }

    [Fact]
    public void AddApiDefaults_WhenLocalPermissiveModeIsEnabledInLocalEnvironment_ShouldAllowDynamicProxy()
    {
        IServiceCollection services = CreateHostServices();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:EnableLocalPermissiveMode"] = "true",
            ["ForwardedHeaders:AllowedHosts:0"] = "ledger.localhost",
            ["ForwardedHeaders:AllowedHosts:1"] = "localhost"
        });

        services.AddApiDefaults<TestExceptionHandler>(configuration);

        using ServiceProvider provider = BuildValidatedProvider(services, "Local");

        TrustedForwardedHeadersOptions trusted = provider.GetRequiredService<IOptions<TrustedForwardedHeadersOptions>>().Value;
        ForwardedHeadersOptions forwarded = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.True(trusted.EnableLocalPermissiveMode);
        Assert.Empty(forwarded.KnownIPNetworks);
        Assert.Empty(forwarded.KnownProxies);
        Assert.Contains("ledger.localhost", forwarded.AllowedHosts);
    }

    [Fact]
    public void AddApiDefaults_WhenDevelopmentConfiguresLocalHosts_ShouldPopulateAllowedHosts()
    {
        IServiceCollection services = CreateHostServices();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:AllowedHosts:0"] = "ledger.localhost",
            ["ForwardedHeaders:AllowedHosts:1"] = "localhost"
        });

        services.AddApiDefaults<TestExceptionHandler>(configuration);

        using ServiceProvider provider = BuildValidatedProvider(services, "Development");

        ForwardedHeadersOptions forwarded = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.Contains("ledger.localhost", forwarded.AllowedHosts);
        Assert.Contains("localhost", forwarded.AllowedHosts);
    }

    [Fact]
    public void AddApiDefaults_WhenComposeLocalEnablesPermissiveMode_ShouldAllowLocalForwardedHosts()
    {
        IServiceCollection services = CreateHostServices();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:EnableLocalPermissiveMode"] = "true",
            ["ForwardedHeaders:AllowedHosts:0"] = "balance.localhost",
            ["ForwardedHeaders:AllowedHosts:1"] = "localhost"
        });

        services.AddApiDefaults<TestExceptionHandler>(configuration);

        using ServiceProvider provider = BuildValidatedProvider(services, "Development");

        TrustedForwardedHeadersOptions trusted = provider.GetRequiredService<IOptions<TrustedForwardedHeadersOptions>>().Value;
        ForwardedHeadersOptions forwarded = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.True(trusted.EnableLocalPermissiveMode);
        Assert.Contains("balance.localhost", forwarded.AllowedHosts);
        Assert.Empty(forwarded.KnownProxies);
        Assert.Empty(forwarded.KnownIPNetworks);
    }

    [Fact]
    public void AddApiDefaults_WhenNonLocalEnvironmentHasNoProxyOrNetwork_ShouldFailOptionsValidation()
    {
        IServiceCollection services = CreateHostServices();

        services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:AllowedHosts:0"] = "api.example.com"
        }));

        using ServiceProvider provider = BuildValidatedProvider(services, "Production");

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<TrustedForwardedHeadersOptions>>().Value);
        Assert.Contains("trusted proxy or trusted network", string.Join(" ", exception.Failures));
    }

    [Fact]
    public void AddApiDefaults_WhenNonLocalEnvironmentHasNoAllowedHost_ShouldFailOptionsValidation()
    {
        IServiceCollection services = CreateHostServices();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:TrustedProxies:0"] = "10.0.0.10"
        });

        services.AddApiDefaults<TestExceptionHandler>(configuration);

        using ServiceProvider provider = BuildValidatedProvider(services, "Production");

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<TrustedForwardedHeadersOptions>>().Value);
        Assert.Contains("allowed forwarded host", string.Join(" ", exception.Failures));
    }

    [Fact]
    public void AddApiDefaults_WhenNonLocalEnvironmentUsesOnlyLocalhost_ShouldFailOptionsValidation()
    {
        IServiceCollection services = CreateHostServices();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:TrustedProxies:0"] = "10.0.0.10",
            ["ForwardedHeaders:AllowedHosts:0"] = "localhost"
        });

        services.AddApiDefaults<TestExceptionHandler>(configuration);

        using ServiceProvider provider = BuildValidatedProvider(services, "Production");

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<TrustedForwardedHeadersOptions>>().Value);
        Assert.Contains("cannot use local host", string.Join(" ", exception.Failures));
    }

    [Fact]
    public void AddApiDefaults_WhenNonLocalEnvironmentUsesLocalhostSubdomain_ShouldFailOptionsValidation()
    {
        IServiceCollection services = CreateHostServices();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:TrustedProxies:0"] = "10.0.0.10",
            ["ForwardedHeaders:AllowedHosts:0"] = "ledger.localhost"
        });

        services.AddApiDefaults<TestExceptionHandler>(configuration);

        using ServiceProvider provider = BuildValidatedProvider(services, "Production");

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<TrustedForwardedHeadersOptions>>().Value);
        Assert.Contains("cannot use local host", string.Join(" ", exception.Failures));
    }

    [Fact]
    public void AddApiDefaults_WhenTrustedProxyIsConfigured_ShouldPopulateKnownProxies()
    {
        IServiceCollection services = CreateHostServices();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:TrustedProxies:0"] = "10.0.0.10",
            ["ForwardedHeaders:AllowedHosts:0"] = "api.example.com"
        });

        services.AddApiDefaults<TestExceptionHandler>(configuration);

        using ServiceProvider provider = BuildValidatedProvider(services, "Production");

        ForwardedHeadersOptions forwarded = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.Contains(IPAddress.Parse("10.0.0.10"), forwarded.KnownProxies);
        Assert.Empty(forwarded.KnownIPNetworks);
    }

    [Fact]
    public void AddApiDefaults_WhenTrustedNetworkIsConfigured_ShouldPopulateKnownNetworks()
    {
        IServiceCollection services = CreateHostServices();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:TrustedNetworks:0"] = "10.0.0.0/24",
            ["ForwardedHeaders:AllowedHosts:0"] = "api.example.com"
        });

        services.AddApiDefaults<TestExceptionHandler>(configuration);

        using ServiceProvider provider = BuildValidatedProvider(services, "Production");

        ForwardedHeadersOptions forwarded = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.Contains(forwarded.KnownIPNetworks, network =>
            network.BaseAddress.Equals(IPAddress.Parse("10.0.0.0")) && network.PrefixLength == 24);
        Assert.Empty(forwarded.KnownProxies);
    }

    [Fact]
    public void AddApiDefaults_WhenTrustedNetworkCidrIsInvalid_ShouldFailOptionsValidation()
    {
        IServiceCollection services = CreateHostServices();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:TrustedNetworks:0"] = "10.0.0.0/129",
            ["ForwardedHeaders:AllowedHosts:0"] = "api.example.com"
        });

        services.AddApiDefaults<TestExceptionHandler>(configuration);

        using ServiceProvider provider = BuildValidatedProvider(services, "Production");

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<TrustedForwardedHeadersOptions>>().Value);
        Assert.Contains("invalid CIDR", string.Join(" ", exception.Failures));
    }

    [Fact]
    public void AddApiDefaults_WhenConfigurationOmitsOptions_ShouldUseDefaults()
    {
        IServiceCollection services = CreateHostServices();

        services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration());

        using ServiceProvider provider = BuildValidatedProvider(services);

        ApiDefaultsOptions options = provider.GetRequiredService<IOptions<ApiDefaultsOptions>>().Value;
        Assert.Equal(1_048_576, options.MaxRequestBodySizeBytes);
        Assert.Equal(100, options.RateLimitPermitLimit);
        Assert.Equal(60, options.RateLimitWindowSeconds);
        Assert.Equal(10, options.RateLimitQueueLimit);
    }

    [Fact]
    public void AddApiDefaults_WhenPolicyRateLimitIsInvalid_ShouldFailOptionsValidation()
    {
        IServiceCollection services = CreateHostServices();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ApiLimits:AuthenticatedReadRateLimit:PermitLimit"] = "0"
        });

        services.AddApiDefaults<TestExceptionHandler>(configuration);

        using ServiceProvider provider = BuildValidatedProvider(services);

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ApiDefaultsOptions>>().Value);
        Assert.Contains("policy rate limits", string.Join(" ", exception.Failures));
    }

    [Fact]
    public void AddApiDefaults_ShouldConfigureCorsAndApiVersioningDefaults()
    {
        IServiceCollection services = CreateHostServices();

        services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration());

        using ServiceProvider provider = BuildValidatedProvider(services);

        var cors = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>>().Value;
        Assert.NotNull(cors.GetPolicy(ApiDefaultsServiceCollectionExtensions.CorsPolicyName));

        ApiVersioningOptions versioning = provider.GetRequiredService<IOptions<ApiVersioningOptions>>().Value;
        Assert.Equal(new ApiVersion(1, 0), versioning.DefaultApiVersion);
        Assert.True(versioning.AssumeDefaultVersionWhenUnspecified);
        Assert.True(versioning.ReportApiVersions);
        Assert.IsType<UrlSegmentApiVersionReader>(versioning.ApiVersionReader);
    }

    [Fact]
    public void AddApiSwaggerDefaults_ShouldRegisterSwaggerAndDocumentFilter()
    {
        IServiceCollection services = CreateHostServices();

        services.AddApiSwaggerDefaults<TestConfigureSwaggerOptions>(
            typeof(ApiDefaultsServiceCollectionExtensionsTests).Assembly,
            options => options.CustomSchemaIds(type => $"custom-{type.Name}"));

        using ServiceProvider provider = BuildProvider(services);

        IEnumerable<IConfigureOptions<SwaggerGenOptions>> configureOptions = provider.GetServices<IConfigureOptions<SwaggerGenOptions>>();
        Assert.Contains(configureOptions, item => item.GetType() == typeof(TestConfigureSwaggerOptions));

        SwaggerGenOptions swaggerOptions = provider.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;
        Assert.Contains(swaggerOptions.DocumentFilterDescriptors, descriptor =>
            descriptor.Type == typeof(ApiDefaults.Swagger.OpenApiContractQualityDocumentFilter));
    }

    private static ServiceProvider BuildValidatedProvider(IServiceCollection services, string environmentName = "Development")
    {
        services.AddLogging();
        services.AddControllers();
        services.RemoveAll<IWebHostEnvironment>();
        services.RemoveAll<IHostEnvironment>();
        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment
        {
            EnvironmentName = environmentName
        });
        services.AddSingleton<IHostEnvironment>(provider => provider.GetRequiredService<IWebHostEnvironment>());
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static ServiceProvider BuildProvider(IServiceCollection services)
    {
        services.AddLogging();
        services.TryAddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment());
        services.TryAddSingleton<IHostEnvironment>(provider => provider.GetRequiredService<IWebHostEnvironment>());
        return services.BuildServiceProvider();
    }

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?>? values = null)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();

    private static IServiceCollection CreateHostServices()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Test"
        });
        return builder.Services;
    }

    private sealed class TestExceptionHandler : IExceptionHandler
    {
        public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
            => ValueTask.FromResult(false);
    }

    private sealed class TestConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
    {
        public void Configure(SwaggerGenOptions options)
        {
            options.SupportNonNullableReferenceTypes();
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ApplicationName { get; set; } = "ApiDefaults.Tests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public string EnvironmentName { get; set; } = Environments.Development;
    }
}
