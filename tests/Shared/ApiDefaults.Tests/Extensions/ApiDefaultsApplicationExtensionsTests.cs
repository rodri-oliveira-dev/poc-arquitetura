using System.Net;
using System.Text;

using ApiDefaults.Extensions;
using ApiDefaults.Middlewares;

using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
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
        builder.Services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration());
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
        Assert.Equal(SecurityHeadersMiddleware.ApiContentSecurityPolicy, response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal(expectHsts, response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task UseApiDefaults_ShouldHandleExceptionsWithRegisteredHandler()
    {
        WebApplicationBuilder builder = CreateBuilder("Test");
        builder.Services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration());
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
        builder.Services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration());
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
        Assert.True(exception.Headers.Contains(CorrelationIdMiddleware.HeaderName));
        Assert.Equal("nosniff", exception.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task ApiDefaultsIntegration_WhenValidationErrorOccurs_ShouldKeepSecurityAndCorrelationHeaders()
    {
        WebApplicationBuilder builder = CreateBuilder("Test");
        builder.Services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration());
        await using WebApplication app = builder.Build();

        app.UseApiDefaults();
        app.MapPost("/validate", () => Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["name"] = ["The name field is required."]
        }));
        await app.StartAsync(TestContext.Current.CancellationToken);

        using StringContent content = new("{}", Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await app.GetTestClient()
            .PostAsync("/validate", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(response.Headers.Contains(CorrelationIdMiddleware.HeaderName));
        Assert.Equal(SecurityHeadersMiddleware.ApiContentSecurityPolicy, response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task UseForwardedHeaders_WhenProxyIsTrusted_ShouldApplyForwardedProtoHostAndClientIp()
    {
        using HttpResponseMessage response = await SendForwardedRequestAsync(
            remoteIpAddress: "10.0.0.10",
            configuration: CreateConfiguration(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:TrustedProxies:0"] = "10.0.0.10"
            }),
            forwardedFor: "203.0.113.9",
            forwardedProto: "https",
            forwardedHost: "api.example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "https|api.example.com|203.0.113.9",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UseForwardedHeaders_WhenProxyIsNotTrusted_ShouldIgnoreForwardedHeaders()
    {
        using HttpResponseMessage response = await SendForwardedRequestAsync(
            remoteIpAddress: "10.0.0.99",
            configuration: CreateConfiguration(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:TrustedProxies:0"] = "10.0.0.10"
            }),
            forwardedFor: "203.0.113.9",
            forwardedProto: "https",
            forwardedHost: "api.example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "http|internal.local|10.0.0.99",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UseForwardedHeaders_WhenRemoteIpMatchesTrustedNetwork_ShouldApplyForwardedHeaders()
    {
        using HttpResponseMessage response = await SendForwardedRequestAsync(
            remoteIpAddress: "10.0.0.25",
            configuration: CreateConfiguration(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:TrustedNetworks:0"] = "10.0.0.0/24"
            }),
            forwardedFor: "203.0.113.10",
            forwardedProto: "https",
            forwardedHost: "api.example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "https|api.example.com|203.0.113.10",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UseForwardedHeaders_WhenClientSpoofsSchemeHostOrIpDirectly_ShouldIgnoreForwardedHeaders()
    {
        using HttpResponseMessage response = await SendForwardedRequestAsync(
            remoteIpAddress: "198.51.100.50",
            configuration: CreateConfiguration(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:TrustedProxies:0"] = "10.0.0.10"
            }),
            forwardedFor: "203.0.113.99",
            forwardedProto: "https",
            forwardedHost: "admin.example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "http|internal.local|198.51.100.50",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UseForwardedHeaders_WhenForwardedForHasMultipleHops_ShouldHonorForwardLimitOne()
    {
        using HttpResponseMessage response = await SendForwardedRequestAsync(
            remoteIpAddress: "10.0.0.10",
            configuration: CreateConfiguration(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:TrustedProxies:0"] = "10.0.0.10"
            }),
            forwardedFor: "198.51.100.1, 203.0.113.9",
            forwardedProto: "https",
            forwardedHost: "api.example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "https|api.example.com|203.0.113.9",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UseForwardedHeaders_WhenForwardedHostIsAllowed_ShouldApplyHost()
    {
        using HttpResponseMessage response = await SendForwardedRequestAsync(
            remoteIpAddress: "10.0.0.10",
            configuration: CreateConfiguration(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:TrustedProxies:0"] = "10.0.0.10",
                ["ForwardedHeaders:AllowedHosts:0"] = "api.example.com"
            }),
            forwardedFor: "203.0.113.9",
            forwardedProto: "https",
            forwardedHost: "api.example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "https|api.example.com|203.0.113.9",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UseForwardedHeaders_WhenForwardedHostIsRejected_ShouldKeepOriginalHost()
    {
        using HttpResponseMessage response = await SendForwardedRequestAsync(
            remoteIpAddress: "10.0.0.10",
            configuration: CreateConfiguration(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:TrustedProxies:0"] = "10.0.0.10",
                ["ForwardedHeaders:AllowedHosts:0"] = "api.example.com"
            }),
            forwardedFor: "203.0.113.9",
            forwardedProto: "https",
            forwardedHost: "evil.example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "https|internal.local|203.0.113.9",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
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
    public async Task UseApiSwaggerDefaults_WhenDisabledOutsideDevelopment_ShouldNotServeSwagger()
    {
        WebApplicationBuilder builder = CreateBuilder("Production");
        builder.Services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration());
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        await using WebApplication app = builder.Build();

        app.UseApiDefaults();
        WebApplication returned = app.UseApiSwaggerDefaults(CreateConfiguration(), "Shared API");
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpResponseMessage response = await app.GetTestClient()
            .GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        Assert.Same(app, returned);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
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
        builder.Services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration());
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSingleton<IApiVersionDescriptionProvider>(
            new TestApiVersionDescriptionProvider(
            [
                new ApiVersionDescription(new ApiVersion(1, 0), "v1", false, null, null)
            ]));
        await using WebApplication app = builder.Build();

        app.UseApiDefaults();
        app.UseApiSwaggerDefaults(configuration, "Shared API");
        app.MapGet("/api/v1/ping", () => Results.Ok(new { status = "ok" }));
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpResponseMessage response = await app.GetTestClient()
            .GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(SecurityHeadersMiddleware.ApiContentSecurityPolicy, response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.DoesNotContain("unsafe-inline", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.True(response.Headers.Contains(CorrelationIdMiddleware.HeaderName));
    }

    [Fact]
    public async Task UseApiSwaggerDefaults_WhenEnabled_ShouldServeSwaggerUiWithDocumentationCsp()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Swagger:Enabled"] = "true"
        });
        WebApplicationBuilder builder = CreateBuilder("Test", new Dictionary<string, string?>
        {
            ["Swagger:Enabled"] = "true"
        });
        builder.Services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration());
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSingleton<IApiVersionDescriptionProvider>(
            new TestApiVersionDescriptionProvider(
            [
                new ApiVersionDescription(new ApiVersion(1, 0), "v1", false, null, null)
            ]));
        await using WebApplication app = builder.Build();

        app.UseApiDefaults();
        app.UseApiSwaggerDefaults(configuration, "Shared API");
        app.MapGet("/api/v1/ping", () => Results.Ok(new { status = "ok" }));
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpResponseMessage response = await app.GetTestClient()
            .GetAsync("/swagger/index.html", TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Swagger UI", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(SecurityHeadersMiddleware.SwaggerUiContentSecurityPolicy, response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("unsafe-inline", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseApiDefaults_ShouldNotDuplicateSecurityHeaders()
    {
        WebApplicationBuilder builder = CreateBuilder("Test");
        builder.Services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration());
        await using WebApplication app = builder.Build();

        app.UseApiDefaults();
        app.MapGet("/ok", () => Results.Ok());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpResponseMessage response = await app.GetTestClient().GetAsync("/ok", TestContext.Current.CancellationToken);

        Assert.Single(response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Single(response.Headers.GetValues("X-Frame-Options"));
        Assert.Single(response.Headers.GetValues("Referrer-Policy"));
        Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
    }

    [Fact]
    public async Task UseApiDefaults_WhenEnvironmentAllowsHttpsRedirection_ShouldRedirectHttpRequests()
    {
        WebApplicationBuilder builder = CreateBuilder("Production");
        builder.Services.AddApiDefaults<TestExceptionHandler>(CreateConfiguration());
        builder.Services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = 443);
        await using WebApplication app = builder.Build();

        app.UseApiDefaults();
        app.MapGet("/ok", () => Results.Ok());
        await app.StartAsync(TestContext.Current.CancellationToken);

        HttpClient client = app.GetTestClient();
        client.BaseAddress = new Uri("http://localhost");
        using HttpResponseMessage response = await client.GetAsync("/ok", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal(new Uri("https://localhost/ok"), response.Headers.Location);
        Assert.True(response.Headers.Contains(CorrelationIdMiddleware.HeaderName));
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

    private static async Task<HttpResponseMessage> SendForwardedRequestAsync(
        string remoteIpAddress,
        IConfiguration configuration,
        string forwardedFor,
        string forwardedProto,
        string forwardedHost)
    {
        WebApplicationBuilder builder = CreateBuilder("Test");
        builder.Services.AddApiDefaults<TestExceptionHandler>(configuration);
        await using WebApplication app = builder.Build();

        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIpAddress);
            return next(context);
        });
        app.UseForwardedHeaders();
        app.MapGet("/forwarded", (HttpContext context) =>
            Results.Text($"{context.Request.Scheme}|{context.Request.Host}|{context.Connection.RemoteIpAddress}"));
        await app.StartAsync(TestContext.Current.CancellationToken);

        HttpClient client = app.GetTestClient();
        client.BaseAddress = new Uri("http://internal.local");
        using HttpRequestMessage request = new(HttpMethod.Get, "/forwarded");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", forwardedProto);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", forwardedHost);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?>? values = null)
    {
        Dictionary<string, string?> effectiveValues = values is null
            ? []
            : new Dictionary<string, string?>(values);

        if (!effectiveValues.Keys.Any(key => key.StartsWith("ForwardedHeaders:", StringComparison.Ordinal)))
        {
            effectiveValues["ForwardedHeaders:TrustedProxies:0"] = "127.0.0.1";
        }

        if (!effectiveValues.Keys.Any(key => key.StartsWith("ForwardedHeaders:AllowedHosts:", StringComparison.Ordinal)))
        {
            effectiveValues["ForwardedHeaders:AllowedHosts:0"] = "api.example.com";
            effectiveValues["ForwardedHeaders:AllowedHosts:1"] = "internal.local";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(effectiveValues)
            .Build();
    }

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
