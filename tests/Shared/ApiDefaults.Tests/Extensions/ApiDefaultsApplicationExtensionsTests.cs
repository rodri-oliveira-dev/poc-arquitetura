using System.Net;

using ApiDefaults.Extensions;
using ApiDefaults.Middlewares;

using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiDefaults.Tests.Extensions;

public sealed class ApiDefaultsApplicationExtensionsTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", true)]
    public async Task UseApiDefaults_ShouldConfigurePipelineForEnvironment(string environment, bool expectHsts)
    {
        WebApplicationBuilder builder = CreateBuilder(environment);
        builder.Services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration(), "localhost");
        await using WebApplication app = builder.Build();

        app.UseApiDefaults();
        app.MapGet("/ok", () => Results.Ok(new { status = "ok" }));
        await app.StartAsync(TestContext.Current.CancellationToken);

        HttpClient client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        using HttpRequestMessage request = new(HttpMethod.Get, "/ok");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "11111111-1111-1111-1111-111111111111");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("11111111-1111-1111-1111-111111111111", response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal(expectHsts, response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task UseApiDefaults_ShouldHandleExceptionsWithRegisteredHandler()
    {
        WebApplicationBuilder builder = CreateBuilder("Test");
        builder.Services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration(), "localhost");
        await using WebApplication app = builder.Build();

        app.UseApiDefaults();
        app.MapGet("/boom", () =>
        {
            throw new InvalidOperationException("boom");
        });
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpResponseMessage response = await app.GetTestClient().GetAsync("/boom", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("handled", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ApiDefaultsIntegration_ShouldStartServeEndpointHealthHeadersExceptionAndAuthorization()
    {
        WebApplicationBuilder builder = CreateBuilder("Test");
        builder.Services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration(), "localhost");
        await using WebApplication app = builder.Build();

        app.UseApiDefaults();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapApiHealthEndpoints((_, _) => Task.FromResult(true), "Readiness de teste.");
        app.MapGet("/secure", () => Results.Ok("secure")).RequireAuthorization();
        app.MapGet("/boom", () =>
        {
            throw new InvalidOperationException("boom");
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        HttpClient client = app.GetTestClient();

        using HttpResponseMessage health = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.True(health.Headers.Contains(CorrelationIdMiddleware.HeaderName));
        Assert.Equal("nosniff", health.Headers.GetValues("X-Content-Type-Options").Single());

        using HttpResponseMessage authorized = await client.GetAsync("/secure", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);

        using HttpRequestMessage deniedRequest = new(HttpMethod.Get, "/secure");
        deniedRequest.Headers.Add("X-Test-Auth", "deny");
        using HttpResponseMessage denied = await client.SendAsync(deniedRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

        using HttpResponseMessage exception = await client.GetAsync("/boom", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal("handled", await exception.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ConfigureApiDefaults_ShouldDisableServerHeaderAndApplyConfiguredBodyLimit()
    {
        WebApplicationBuilder builder = CreateBuilder("Test", new Dictionary<string, string?>
        {
            ["ApiLimits:MaxRequestBodySizeBytes"] = "4096"
        });

        builder.WebHost.ConfigureApiDefaults();
        using WebApplication app = builder.Build();

        Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions options =
            app.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>>().Value;
        Assert.False(options.AddServerHeader);
        Assert.Equal(4096, options.Limits.MaxRequestBodySize);
    }

    [Fact]
    public void ConfigureApiDefaults_WhenBodyLimitIsMissing_ShouldKeepKestrelDefaultLimit()
    {
        WebApplicationBuilder builder = CreateBuilder("Test");

        builder.WebHost.ConfigureApiDefaults();
        using WebApplication app = builder.Build();

        Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions options =
            app.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>>().Value;
        Assert.False(options.AddServerHeader);
        Assert.NotEqual(0, options.Limits.MaxRequestBodySize);
    }

    [Fact]
    public async Task UseApiSwaggerDefaults_WhenDisabledOutsideDevelopment_ShouldReturnAppWithoutSwaggerServices()
    {
        WebApplicationBuilder builder = CreateBuilder("Production");
        await using WebApplication app = builder.Build();

        WebApplication returned = app.UseApiSwaggerDefaults(CreateConfiguration(), "Shared API");

        Assert.Same(app, returned);
    }

    [Fact]
    public async Task UseApiSwaggerDefaults_WhenEnabled_ShouldConfigureSwaggerUiForVersions()
    {
        WebApplicationBuilder builder = CreateBuilder("Test", new Dictionary<string, string?>
        {
            ["Swagger:Enabled"] = "true"
        });
        builder.Services.AddSwaggerGen();
        builder.Services.AddSingleton<IApiVersionDescriptionProvider>(
            new TestApiVersionDescriptionProvider(
            [
                new ApiVersionDescription(new ApiVersion(1, 0), "v1", false, null, null),
                new ApiVersionDescription(new ApiVersion(2, 0), "v2", true, null, null)
            ]));
        await using WebApplication app = builder.Build();

        WebApplication returned = app.UseApiSwaggerDefaults(CreateConfiguration(new Dictionary<string, string?>
        {
            ["Swagger:Enabled"] = "true"
        }), "Shared API");

        Assert.Same(app, returned);
    }

    [Fact]
    public async Task UseApiSwaggerDefaults_WhenEnabled_ShouldAddSecurityHeadersToSwaggerJson()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Swagger:Enabled"] = "true"
        });
        WebApplicationBuilder builder = CreateBuilder("Test", new Dictionary<string, string?>
        {
            ["Swagger:Enabled"] = "true"
        });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSingleton<IApiVersionDescriptionProvider>(
            new TestApiVersionDescriptionProvider(
            [
                new ApiVersionDescription(new ApiVersion(1, 0), "v1", false, null, null)
            ]));
        await using WebApplication app = builder.Build();

        app.UseApiSwaggerDefaults(configuration, "Shared API");
        app.MapGet("/api/v1/ping", () => Results.Ok(new { status = "ok" }));
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpResponseMessage response = await app.GetTestClient()
            .GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public void UseApiDefaults_WhenAppIsNull_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>("app", () =>
            ApiDefaultsApplicationExtensions.UseApiDefaults(null!));
    }

    [Fact]
    public async Task UseApiSwaggerDefaults_WhenArgumentsAreNull_ShouldThrow()
    {
        WebApplicationBuilder builder = CreateBuilder("Test");
        await using WebApplication app = builder.Build();

        Assert.Throws<ArgumentNullException>("app", () =>
            ApiDefaultsApplicationExtensions.UseApiSwaggerDefaults(null!, CreateConfiguration(), "Shared API"));
        Assert.Throws<ArgumentNullException>("configuration", () =>
            app.UseApiSwaggerDefaults(null!, "Shared API"));
    }

    private static WebApplicationBuilder CreateBuilder(
        string environment,
        IReadOnlyDictionary<string, string?>? configuration = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(configuration ?? new Dictionary<string, string?>());
        return builder;
    }

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?>? values = null)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();

    private sealed class TestExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await httpContext.Response.WriteAsync("handled", cancellationToken);
            return true;
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.TryGetValue("X-Test-Auth", out var value) && value == "deny")
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "test-user")],
                Scheme.Name);
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    private sealed class TestApiVersionDescriptionProvider(IReadOnlyList<ApiVersionDescription> descriptions) : IApiVersionDescriptionProvider
    {
        public IReadOnlyList<ApiVersionDescription> ApiVersionDescriptions
        {
            get;
        } = descriptions;
    }
}
