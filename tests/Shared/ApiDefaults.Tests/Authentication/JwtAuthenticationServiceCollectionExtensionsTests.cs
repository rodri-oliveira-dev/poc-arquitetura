using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;

using ApiDefaults.Authentication;
using ApiDefaults.Extensions;
using ApiDefaults.Security;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ApiDefaults.Tests.Authentication;

public sealed class JwtAuthenticationServiceCollectionExtensionsTests
{
    private const string PolicyName = "payments.read";

    [Fact]
    public async Task AddApiJwtBearerAuthentication_should_register_effective_authentication_and_jwt_options_Async()
    {
        ServiceCollection services = CreateServices();

        services.AddApiJwtBearerAuthentication(
            ApiJwtAuthenticationOptionsTests.ValidOptions() with
            {
                ClockSkewSeconds = 45
            },
            new ApiJwtAuthenticationOptionsTests.TestHostEnvironment("Production"),
            options => options.AddPolicy(PolicyName, policy => policy.RequireScope("scope", PolicyName)));

        using ServiceProvider provider = services.BuildServiceProvider();

        var authenticationOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        JwtBearerOptions jwtOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        AuthorizationOptions authorizationOptions = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        TokenValidationParameters tokenValidationParameters = jwtOptions.TokenValidationParameters;

        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, authenticationOptions.DefaultScheme);
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, (await schemeProvider.GetDefaultAuthenticateSchemeAsync())?.Name);
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, (await schemeProvider.GetDefaultChallengeSchemeAsync())?.Name);
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, (await schemeProvider.GetDefaultForbidSchemeAsync())?.Name);
        Assert.True(jwtOptions.RequireHttpsMetadata);
        Assert.Null(jwtOptions.Authority);
        Assert.Null(jwtOptions.Audience);
        Assert.NotNull(jwtOptions.ConfigurationManager);
        Assert.IsType<ConfigurationManager<OpenIdConnectConfiguration>>(jwtOptions.ConfigurationManager);
        Assert.True(tokenValidationParameters.ValidateIssuer);
        Assert.Equal("https://issuer.example", tokenValidationParameters.ValidIssuer);
        Assert.True(tokenValidationParameters.ValidateAudience);
        Assert.True(tokenValidationParameters.AudienceValidator?.Invoke(["api://payments"], null, tokenValidationParameters));
        Assert.True(tokenValidationParameters.AudienceValidator?.Invoke(["api://other api://payments"], null, tokenValidationParameters));
        Assert.False(tokenValidationParameters.AudienceValidator?.Invoke(["api://payments.all"], null, tokenValidationParameters));
        Assert.False(tokenValidationParameters.AudienceValidator?.Invoke(null, null, tokenValidationParameters));
        Assert.True(tokenValidationParameters.ValidateLifetime);
        Assert.True(tokenValidationParameters.ValidateIssuerSigningKey);
        Assert.Equal(TimeSpan.FromSeconds(45), tokenValidationParameters.ClockSkew);
        Assert.Contains(SecurityAlgorithms.RsaSha256, tokenValidationParameters.ValidAlgorithms);
        Assert.NotNull(jwtOptions.Events);
        Assert.NotNull(jwtOptions.Events.OnAuthenticationFailed);
        Assert.NotNull(authorizationOptions.GetPolicy(PolicyName));
        Assert.Contains(provider.GetServices<IValidateOptions<ApiJwtAuthenticationOptions>>(), value => value is ApiJwtAuthenticationOptionsValidator);
        Assert.NotEmpty(provider.GetServices<IConfigureOptions<JwtBearerOptions>>());
        Assert.NotEmpty(provider.GetServices<IPostConfigureOptions<JwtBearerOptions>>());
        Assert.NotNull(provider.GetRequiredService<IHttpClientFactory>().CreateClient(JwtAuthenticationServiceCollectionExtensions.JwksHttpClientName));
    }

    [Fact]
    public void AddApiJwtBearerAuthentication_should_throw_when_configuration_is_invalid()
    {
        ServiceCollection services = CreateServices();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => services.AddApiJwtBearerAuthentication(
                ApiJwtAuthenticationOptionsTests.ValidOptions() with
                {
                    Issuer = "",
                    RequireHttpsMetadata = false,
                    ClockSkewSeconds = -1
                },
                new ApiJwtAuthenticationOptionsTests.TestHostEnvironment("Production"),
                _ => { }));

        Assert.Contains("Jwt:Issuer e obrigatorio.", exception.Message);
        Assert.Contains("Jwt:RequireHttpsMetadata=false e permitido apenas em Development/Local/Test.", exception.Message);
        Assert.Contains("Jwt:ClockSkewSeconds nao pode ser negativo.", exception.Message);
    }

    [Fact]
    public void AddConfiguredApiJwtBearerAuthentication_should_read_and_trim_configuration_values()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = " https://issuer.example ",
                ["Jwt:Audience"] = " api://payments ",
                ["Jwt:JwksUrl"] = " https://issuer.example/.well-known/jwks.json ",
                ["Jwt:RequireHttpsMetadata"] = "true",
                ["Jwt:JwksTimeoutSeconds"] = "7",
                ["Jwt:JwksRetryCount"] = "3",
                ["Jwt:JwksRetryBaseDelayMilliseconds"] = "250",
                ["Jwt:ClockSkewSeconds"] = "12"
            })
            .Build();
        ServiceCollection services = CreateServices();

        services.AddConfiguredApiJwtBearerAuthentication<ITestService, TestService>(
            configuration,
            new ApiJwtAuthenticationOptionsTests.TestHostEnvironment("Production"),
            "Jwt",
            options => options.RequireAuthenticatedUserByDefault());

        using ServiceProvider provider = services.BuildServiceProvider();
        JwtBearerOptions jwtOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        AuthorizationOptions authorizationOptions = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        Assert.IsType<TestService>(provider.GetRequiredService<ITestService>());
        Assert.Equal("https://issuer.example", jwtOptions.TokenValidationParameters.ValidIssuer);
        Assert.True(jwtOptions.TokenValidationParameters.AudienceValidator?.Invoke(["api://payments"], null, jwtOptions.TokenValidationParameters));
        Assert.Equal(TimeSpan.FromSeconds(12), jwtOptions.TokenValidationParameters.ClockSkew);
        Assert.NotNull(authorizationOptions.FallbackPolicy);
        Assert.True(authorizationOptions.FallbackPolicy.Requirements.OfType<DenyAnonymousAuthorizationRequirement>().Any());
    }

    [Fact]
    public async Task Jwt_bearer_events_should_log_authentication_failures_without_token_content_Async()
    {
        ServiceCollection services = CreateServices();
        services.AddSingleton<ILoggerFactory, CapturingLoggerFactory>();
        services.AddApiJwtBearerAuthentication(
            ApiJwtAuthenticationOptionsTests.ValidOptions(),
            new ApiJwtAuthenticationOptionsTests.TestHostEnvironment("Production"),
            _ => { });
        using ServiceProvider provider = services.BuildServiceProvider();
        JwtBearerOptions jwtOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider
        };
        httpContext.Request.Path = "/secure";

        await jwtOptions.Events.OnAuthenticationFailed(new AuthenticationFailedContext(httpContext, new AuthenticationScheme("Bearer", null, typeof(TestAuthenticationHandler)), jwtOptions)
        {
            Exception = new SecurityTokenException("secret-token")
        });

        var loggerFactory = Assert.IsType<CapturingLoggerFactory>(provider.GetRequiredService<ILoggerFactory>());
        CapturingLogger entry = Assert.Single(loggerFactory.Loggers);
        Assert.Contains("JWT authentication failed. path=/secure", entry.Messages.Single());
        Assert.DoesNotContain("secret-token", entry.Messages.Single());
    }

    [Fact]
    public async Task TestServer_should_enforce_registered_scope_policy_with_fake_authentication_Async()
    {
        await using WebApplication app = CreateAuthorizationTestApplication("scope", PolicyName);
        await app.StartAsync(TestContext.Current.CancellationToken);
        HttpClient client = app.GetTestClient();

        HttpResponseMessage withoutToken = await client.GetAsync("/secure", TestContext.Current.CancellationToken);
        HttpResponseMessage withScope = await client.GetAsync("/secure?authenticated=true&scope=payments.read", TestContext.Current.CancellationToken);
        HttpResponseMessage withoutScope = await client.GetAsync("/secure?authenticated=true", TestContext.Current.CancellationToken);
        HttpResponseMessage withWrongScope = await client.GetAsync("/secure?authenticated=true&scope=payments.read.all", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, withoutToken.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withScope.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, withoutScope.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, withWrongScope.StatusCode);
        Assert.Equal("Bearer", withoutToken.Headers.WwwAuthenticate.Single().Scheme);
    }

    private static ServiceCollection CreateServices()
    {
        ServiceCollection services = new();
        services.AddLogging();
        return services;
    }

    private static WebApplication CreateAuthorizationTestApplication(string claimType, string scope)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Test"
        });
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder("Test")
                .RequireAuthenticatedUser()
                .Build();
            options.AddPolicy(scope, policy => policy.AddAuthenticationSchemes("Test").RequireScope(claimType, scope));
        });

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/secure", () => Results.Ok()).RequireAuthorization(scope);
        return app;
    }

    private interface ITestService;

    private sealed class TestService : ITestService;

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Query.TryGetValue("authenticated", out Microsoft.Extensions.Primitives.StringValues authenticated)
                || authenticated != "true")
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            List<Claim> claims = [];
            foreach (string? value in Request.Query["scope"])
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    claims.Add(new Claim("scope", value));
                }
            }

            ClaimsIdentity identity = new(claims, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Headers.WWWAuthenticate = "Bearer";
            return base.HandleChallengeAsync(properties);
        }
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<CapturingLogger> Loggers { get; } = [];

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            var logger = new CapturingLogger();
            Loggers.Add(logger);
            return logger;
        }

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
