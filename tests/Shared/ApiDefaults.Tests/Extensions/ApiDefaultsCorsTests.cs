using System.Net;

using ApiDefaults.Extensions;
using ApiDefaults.Middlewares;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApiDefaults.Tests.Extensions;

public sealed class ApiDefaultsCorsTests
{
    [Fact]
    public async Task UseApiDefaults_WhenCorsIsDisabled_ShouldNotEmitCorsHeaders()
    {
        await using WebApplication app = await CreateStartedAppAsync();

        using HttpRequestMessage request = CreateCorsRequest(HttpMethod.Get, "/resource", "http://localhost:5173");
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task UseApiDefaults_WhenOriginIsAllowed_ShouldEmitAllowedOrigin()
    {
        await using WebApplication app = await CreateStartedAppAsync(CreateCorsConfiguration(
            allowedOrigins: ["http://localhost:5173"],
            allowedMethods: ["GET"],
            allowedHeaders: ["Authorization", "Content-Type", "Idempotency-Key", CorrelationIdMiddleware.HeaderName]));

        using HttpRequestMessage request = CreateCorsRequest(HttpMethod.Get, "/resource", "http://localhost:5173");
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task UseApiDefaults_WhenOriginIsRejected_ShouldNotEmitAllowedOrigin()
    {
        await using WebApplication app = await CreateStartedAppAsync(CreateCorsConfiguration(
            allowedOrigins: ["http://localhost:5173"],
            allowedMethods: ["GET"],
            allowedHeaders: ["Authorization"]));

        using HttpRequestMessage request = CreateCorsRequest(HttpMethod.Get, "/resource", "https://evil.example");
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task UseApiDefaults_WhenMultipleOriginsAreConfigured_ShouldAllowEachConfiguredOrigin()
    {
        await using WebApplication app = await CreateStartedAppAsync(CreateCorsConfiguration(
            allowedOrigins: ["http://localhost:3000", "http://localhost:5173"],
            allowedMethods: ["GET"],
            allowedHeaders: ["Authorization"]));

        using HttpRequestMessage request = CreateCorsRequest(HttpMethod.Get, "/resource", "http://localhost:3000");
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("http://localhost:3000", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task UseApiDefaults_WhenPreflightIsValid_ShouldEmitConfiguredCorsHeaders()
    {
        await using WebApplication app = await CreateStartedAppAsync(CreateCorsConfiguration(
            allowedOrigins: ["http://localhost:5173"],
            allowedMethods: ["POST"],
            allowedHeaders: ["Authorization", "Content-Type", "Idempotency-Key", CorrelationIdMiddleware.HeaderName],
            exposedHeaders: [CorrelationIdMiddleware.HeaderName],
            preflightMaxAgeSeconds: 600));

        using HttpRequestMessage request = CreatePreflightRequest(
            "http://localhost:5173",
            "POST",
            "Authorization, Content-Type, Idempotency-Key, X-Correlation-Id");
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("POST", response.Headers.GetValues("Access-Control-Allow-Methods").Single(), StringComparison.Ordinal);
        Assert.Contains("Authorization", response.Headers.GetValues("Access-Control-Allow-Headers").Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Content-Type", response.Headers.GetValues("Access-Control-Allow-Headers").Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Idempotency-Key", response.Headers.GetValues("Access-Control-Allow-Headers").Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(CorrelationIdMiddleware.HeaderName, response.Headers.GetValues("Access-Control-Allow-Headers").Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("600", response.Headers.GetValues("Access-Control-Max-Age").Single());
    }

    [Fact]
    public async Task UseApiDefaults_WhenPreflightMethodIsNotAllowed_ShouldNotEmitAllowedMethods()
    {
        await using WebApplication app = await CreateStartedAppAsync(CreateCorsConfiguration(
            allowedOrigins: ["http://localhost:5173"],
            allowedMethods: ["GET"],
            allowedHeaders: ["Authorization"]));

        using HttpRequestMessage request = CreatePreflightRequest("http://localhost:5173", "DELETE", "Authorization");
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain("DELETE", response.Headers.GetValues("Access-Control-Allow-Methods").Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseApiDefaults_WhenPreflightHeaderIsNotAllowed_ShouldNotEmitAllowedHeaders()
    {
        await using WebApplication app = await CreateStartedAppAsync(CreateCorsConfiguration(
            allowedOrigins: ["http://localhost:5173"],
            allowedMethods: ["POST"],
            allowedHeaders: ["Authorization"]));

        using HttpRequestMessage request = CreatePreflightRequest("http://localhost:5173", "POST", "X-Unsafe-Header");
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain("X-Unsafe-Header", response.Headers.GetValues("Access-Control-Allow-Headers").Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddApiDefaults_WhenOriginIsMalformed_ShouldFailOptionsValidation()
    {
        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            BuildCorsOptions(CreateCorsConfiguration(
                allowedOrigins: ["localhost:5173"],
                allowedMethods: ["GET"],
                allowedHeaders: ["Authorization"])));

        Assert.Contains("invalid absolute origin", string.Join(" ", exception.Failures), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddApiDefaults_WhenOriginContainsPath_ShouldFailOptionsValidation()
    {
        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            BuildCorsOptions(CreateCorsConfiguration(
                allowedOrigins: ["http://localhost:5173/app"],
                allowedMethods: ["GET"],
                allowedHeaders: ["Authorization"])));

        Assert.Contains("must not include a path", string.Join(" ", exception.Failures), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddApiDefaults_WhenWildcardOriginUsesCredentials_ShouldFailOptionsValidation()
    {
        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            BuildCorsOptions(CreateCorsConfiguration(
                allowedOrigins: ["*"],
                allowedMethods: ["GET"],
                allowedHeaders: ["Authorization"],
                allowCredentials: true)));

        string failures = string.Join(" ", exception.Failures);
        Assert.Contains("wildcard", failures, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AllowCredentials", failures, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseApiDefaults_WhenOriginIsNotAuthorized_ShouldNotEmitAccessControlAllowOrigin()
    {
        await using WebApplication app = await CreateStartedAppAsync(CreateCorsConfiguration(
            allowedOrigins: ["https://app.example"],
            allowedMethods: ["GET"],
            allowedHeaders: ["Authorization"]));

        using HttpRequestMessage request = CreateCorsRequest(HttpMethod.Get, "/resource", "https://admin.example");
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task UseApiDefaults_WhenApiDoesNotNeedBrowserConsumers_ShouldRemainWithoutCors()
    {
        await using WebApplication app = await CreateStartedAppAsync(new Dictionary<string, string?>
        {
            ["Cors:Enabled"] = "true"
        });

        using HttpRequestMessage request = CreateCorsRequest(HttpMethod.Get, "/resource", "http://localhost:5173");
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static async Task<WebApplication> CreateStartedAppAsync(IReadOnlyDictionary<string, string?>? configuration = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Test"
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(configuration ?? new Dictionary<string, string?>());
        builder.Services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration(configuration), "localhost");

        WebApplication app = builder.Build();
        app.UseApiDefaults();
        app.MapGet("/resource", () => Results.Ok(new { status = "ok" }));
        app.MapPost("/resource", () => Results.Ok(new { status = "created" }));
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static void BuildCorsOptions(IReadOnlyDictionary<string, string?> configuration)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Test"
        });
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration(configuration), "localhost");

        using WebApplication app = builder.Build();

        _ = app.Services.GetRequiredService<IOptions<Options.CorsOptions>>().Value;
    }

    private static HttpRequestMessage CreateCorsRequest(HttpMethod method, string requestUri, string origin)
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Add("Origin", origin);
        return request;
    }

    private static HttpRequestMessage CreatePreflightRequest(string origin, string method, string headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/resource");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", headers);
        return request;
    }

    private static Dictionary<string, string?> CreateCorsConfiguration(
        string[] allowedOrigins,
        string[] allowedMethods,
        string[] allowedHeaders,
        string[]? exposedHeaders = null,
        bool enabled = true,
        bool allowCredentials = false,
        int? preflightMaxAgeSeconds = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Cors:Enabled"] = enabled.ToString(),
            ["Cors:AllowCredentials"] = allowCredentials.ToString()
        };

        AddValues(values, "Cors:AllowedOrigins", allowedOrigins);
        AddValues(values, "Cors:AllowedMethods", allowedMethods);
        AddValues(values, "Cors:AllowedHeaders", allowedHeaders);
        AddValues(values, "Cors:ExposedHeaders", exposedHeaders ?? []);

        if (preflightMaxAgeSeconds is { } seconds)
        {
            values["Cors:PreflightMaxAgeSeconds"] = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return values;
    }

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?>? values = null)
    {
        Dictionary<string, string?> effectiveValues = values is null
            ? []
            : new Dictionary<string, string?>(values);

        effectiveValues.TryAdd("ForwardedHeaders:TrustedProxies:0", "127.0.0.1");

        return new ConfigurationBuilder()
            .AddInMemoryCollection(effectiveValues)
            .Build();
    }

    private static void AddValues(Dictionary<string, string?> values, string keyPrefix, string[] items)
    {
        for (int index = 0; index < items.Length; index++)
        {
            values[$"{keyPrefix}:{index}"] = items[index];
        }
    }

    private sealed class TestExceptionHandler : IExceptionHandler
    {
        public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
            => ValueTask.FromResult(false);
    }
}
