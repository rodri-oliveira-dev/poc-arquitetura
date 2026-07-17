using System.Diagnostics.Metrics;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;

using ApiDefaults.Extensions;
using ApiDefaults.RateLimiting;

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

public sealed class ApiDefaultsRateLimitingTests
{
    [Fact]
    public async Task AuthenticatedReadPolicy_WhenSubjectsDiffer_ShouldUseIndependentLimits()
    {
        await using WebApplication app = await CreateRateLimitedAppAsync();
        HttpClient client = app.GetTestClient();

        using HttpResponseMessage firstSubjectFirst = await SendAsync(client, HttpMethod.Get, "/read", subject: "subject-a");
        using HttpResponseMessage firstSubjectSecond = await SendAsync(client, HttpMethod.Get, "/read", subject: "subject-a");
        using HttpResponseMessage secondSubject = await SendAsync(client, HttpMethod.Get, "/read", subject: "subject-b");

        Assert.Equal(HttpStatusCode.OK, firstSubjectFirst.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, firstSubjectSecond.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondSubject.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedReadPolicy_WhenMerchantsDiffer_ShouldUseIndependentLimits()
    {
        await using WebApplication app = await CreateRateLimitedAppAsync();
        HttpClient client = app.GetTestClient();

        using HttpResponseMessage merchantAFirst = await SendAsync(client, HttpMethod.Get, "/read", subject: "subject-a", merchantId: "merchant-a");
        using HttpResponseMessage merchantASecond = await SendAsync(client, HttpMethod.Get, "/read", subject: "subject-a", merchantId: "merchant-a");
        using HttpResponseMessage merchantB = await SendAsync(client, HttpMethod.Get, "/read", subject: "subject-a", merchantId: "merchant-b");

        Assert.Equal(HttpStatusCode.OK, merchantAFirst.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, merchantASecond.StatusCode);
        Assert.Equal(HttpStatusCode.OK, merchantB.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedWritePolicy_WhenSameClientExceedsLimit_ShouldReturnTooManyRequests()
    {
        await using WebApplication app = await CreateRateLimitedAppAsync();
        HttpClient client = app.GetTestClient();

        using HttpResponseMessage first = await SendAsync(client, HttpMethod.Post, "/write", clientId: "client-a");
        using HttpResponseMessage second = await SendAsync(client, HttpMethod.Post, "/write", clientId: "client-a");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedWritePolicy_WhenClientDiffers_ShouldNotBeAffectedByRejectedClient()
    {
        await using WebApplication app = await CreateRateLimitedAppAsync();
        HttpClient client = app.GetTestClient();

        using HttpResponseMessage clientAFirst = await SendAsync(client, HttpMethod.Post, "/write", clientId: "client-a");
        using HttpResponseMessage clientASecond = await SendAsync(client, HttpMethod.Post, "/write", clientId: "client-a");
        using HttpResponseMessage clientB = await SendAsync(client, HttpMethod.Post, "/write", clientId: "client-b");

        Assert.Equal(HttpStatusCode.OK, clientAFirst.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, clientASecond.StatusCode);
        Assert.Equal(HttpStatusCode.OK, clientB.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedReadPolicy_WhenOnlyAuthorizedPartyDiffers_ShouldUseIndependentLimits()
    {
        await using WebApplication app = await CreateRateLimitedAppAsync();
        HttpClient client = app.GetTestClient();

        using HttpResponseMessage azpAFirst = await SendAsync(client, HttpMethod.Get, "/read", authorizedParty: "client-a");
        using HttpResponseMessage azpASecond = await SendAsync(client, HttpMethod.Get, "/read", authorizedParty: "client-a");
        using HttpResponseMessage azpB = await SendAsync(client, HttpMethod.Get, "/read", authorizedParty: "client-b");

        Assert.Equal(HttpStatusCode.OK, azpAFirst.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, azpASecond.StatusCode);
        Assert.Equal(HttpStatusCode.OK, azpB.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedReadPolicy_WhenSubjectAndAuthorizedPartyExist_ShouldPrioritizeSubjectAndIsolateClient()
    {
        await using WebApplication app = await CreateRateLimitedAppAsync();
        HttpClient client = app.GetTestClient();

        using HttpResponseMessage firstUserFirst = await SendAsync(client, HttpMethod.Get, "/read", subject: "user-a", authorizedParty: "client-a");
        using HttpResponseMessage firstUserSecond = await SendAsync(client, HttpMethod.Get, "/read", subject: "user-a", authorizedParty: "client-a");
        using HttpResponseMessage secondUserSameClient = await SendAsync(client, HttpMethod.Get, "/read", subject: "user-b", authorizedParty: "client-a");
        using HttpResponseMessage sameUserSecondClient = await SendAsync(client, HttpMethod.Get, "/read", subject: "user-a", authorizedParty: "client-b");

        Assert.Equal(HttpStatusCode.OK, firstUserFirst.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, firstUserSecond.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondUserSameClient.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sameUserSecondClient.StatusCode);
    }

    [Fact]
    public async Task AnonymousWebhookPolicy_WhenRemoteIpDiffers_ShouldUseIndependentLimits()
    {
        await using WebApplication app = await CreateRateLimitedAppAsync();
        HttpClient client = app.GetTestClient();

        using HttpResponseMessage firstIpFirst = await SendAsync(client, HttpMethod.Post, "/webhook", remoteIp: "203.0.113.10");
        using HttpResponseMessage firstIpSecond = await SendAsync(client, HttpMethod.Post, "/webhook", remoteIp: "203.0.113.10");
        using HttpResponseMessage secondIp = await SendAsync(client, HttpMethod.Post, "/webhook", remoteIp: "203.0.113.11");

        Assert.Equal(HttpStatusCode.OK, firstIpFirst.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, firstIpSecond.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondIp.StatusCode);
    }

    [Fact]
    public async Task AnonymousWebhookPolicy_WhenProxyIsNotTrusted_ShouldIgnoreForwardedFor()
    {
        await using WebApplication app = await CreateRateLimitedAppAsync(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:TrustedProxies:0"] = "10.0.0.10"
        });
        HttpClient client = app.GetTestClient();

        using HttpResponseMessage first = await SendAsync(
            client,
            HttpMethod.Post,
            "/webhook",
            remoteIp: "10.0.0.99",
            forwardedFor: "203.0.113.10");
        using HttpResponseMessage second = await SendAsync(
            client,
            HttpMethod.Post,
            "/webhook",
            remoteIp: "10.0.0.99",
            forwardedFor: "203.0.113.11");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedPolicy_WhenClientClaimsAreMissing_ShouldFallbackWithoutEmptySharedPartition()
    {
        await using WebApplication app = await CreateRateLimitedAppAsync();
        HttpClient client = app.GetTestClient();

        using HttpResponseMessage firstIpFirst = await SendAsync(client, HttpMethod.Get, "/read", remoteIp: "203.0.113.20");
        using HttpResponseMessage firstIpSecond = await SendAsync(client, HttpMethod.Get, "/read", remoteIp: "203.0.113.20");
        using HttpResponseMessage secondIp = await SendAsync(client, HttpMethod.Get, "/read", remoteIp: "203.0.113.21");

        Assert.Equal(HttpStatusCode.OK, firstIpFirst.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, firstIpSecond.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondIp.StatusCode);
    }

    [Fact]
    public async Task DistinctPolicies_ShouldHonorReadWriteAndAdministrativeLimits()
    {
        await using WebApplication app = await CreateRateLimitedAppAsync(new Dictionary<string, string?>
        {
            ["ApiLimits:AuthenticatedReadRateLimit:PermitLimit"] = "1",
            ["ApiLimits:AuthenticatedWriteRateLimit:PermitLimit"] = "2",
            ["ApiLimits:AdministrativeRateLimit:PermitLimit"] = "3"
        });
        HttpClient client = app.GetTestClient();

        HttpStatusCode[] readStatuses = await SendManyAsync(client, HttpMethod.Get, "/read", 2, "subject-a");
        HttpStatusCode[] writeStatuses = await SendManyAsync(client, HttpMethod.Post, "/write", 3, "subject-a");
        HttpStatusCode[] adminStatuses = await SendManyAsync(client, HttpMethod.Post, "/admin", 4, "subject-a");

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.TooManyRequests], readStatuses);
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], writeStatuses);
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], adminStatuses);
    }

    [Fact]
    public async Task RejectedRequest_ShouldReturnTooManyRequestsAndRetryAfter()
    {
        await using WebApplication app = await CreateRateLimitedAppAsync();
        HttpClient client = app.GetTestClient();

        using HttpResponseMessage first = await SendAsync(client, HttpMethod.Get, "/read", subject: "subject-a");
        using HttpResponseMessage rejected = await SendAsync(client, HttpMethod.Get, "/read", subject: "subject-a");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public async Task HealthEndpoints_ShouldNotBeRateLimitedByDefault()
    {
        await using WebApplication app = await CreateRateLimitedAppAsync();
        HttpClient client = app.GetTestClient();

        HttpStatusCode[] statuses = await SendManyAsync(client, HttpMethod.Get, "/health", 3, "subject-a");

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    }

    [Fact]
    public async Task RateLimitMetrics_ShouldUseLowCardinalityLabels()
    {
        var observed = new List<ObservedMetric>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ApiRateLimitMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            observed.Add(new ObservedMetric(
                instrument.Name,
                measurement,
                tags.ToArray()));
        });
        listener.Start();

        await using WebApplication app = await CreateRateLimitedAppAsync();
        HttpClient client = app.GetTestClient();

        using HttpResponseMessage first = await SendAsync(client, HttpMethod.Get, "/read", subject: "subject-sensitive", merchantId: "merchant-sensitive");
        using HttpResponseMessage rejected = await SendAsync(client, HttpMethod.Get, "/read", subject: "subject-sensitive", merchantId: "merchant-sensitive");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);

        ObservedMetric metric = Assert.Single(observed, item => item.Name == "api.rate_limiting.rejected_requests");
        Assert.Equal(1, metric.Measurement);
        Assert.Equal(["partition_type", "policy"], metric.Tags.Select(tag => tag.Key).Order(StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(metric.Tags, tag => string.Equals(tag.Value?.ToString(), "subject-sensitive", StringComparison.Ordinal));
        Assert.DoesNotContain(metric.Tags, tag => string.Equals(tag.Value?.ToString(), "merchant-sensitive", StringComparison.Ordinal));
    }

    private static async Task<WebApplication> CreateRateLimitedAppAsync(IReadOnlyDictionary<string, string?>? configuration = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Test"
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(CreateConfiguration(configuration));
        builder.Services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddApiDefaults<TestExceptionHandler>(builder.Configuration);

        WebApplication app = builder.Build();
        app.Use((context, next) =>
        {
            if (context.Request.Headers.TryGetValue("X-Remote-IP", out var value) &&
                IPAddress.TryParse(value.ToString(), out IPAddress? remoteIpAddress))
            {
                context.Connection.RemoteIpAddress = remoteIpAddress;
            }

            return next(context);
        });
        app.UseForwardedHeaders();
        app.UseApiDefaults();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapApiHealthEndpoints(static (_, _) => Task.FromResult(true), "Readiness de teste.");
        app.MapGet("/read", () => Results.Ok()).RequireAuthorization().RequireRateLimiting(ApiRateLimitPolicies.AuthenticatedRead);
        app.MapPost("/write", () => Results.Ok()).RequireAuthorization().RequireRateLimiting(ApiRateLimitPolicies.AuthenticatedWrite);
        app.MapPost("/admin", () => Results.Ok()).RequireAuthorization().RequireRateLimiting(ApiRateLimitPolicies.Administrative);
        app.MapPost("/webhook", () => Results.Ok()).RequireRateLimiting(ApiRateLimitPolicies.AnonymousWebhook);

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static Dictionary<string, string?> CreateConfiguration(IReadOnlyDictionary<string, string?>? values)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["ForwardedHeaders:TrustedProxies:0"] = "127.0.0.1",
            ["ForwardedHeaders:AllowedHosts:0"] = "api.example.com",
            ["ApiLimits:RateLimitPermitLimit"] = "1",
            ["ApiLimits:RateLimitWindowSeconds"] = "60",
            ["ApiLimits:RateLimitQueueLimit"] = "0"
        };

        if (values is not null)
        {
            foreach ((string key, string? value) in values)
            {
                configuration[key] = value;
            }
        }

        return configuration;
    }

    private static async Task<HttpStatusCode[]> SendManyAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        int count,
        string subject)
    {
        var statuses = new List<HttpStatusCode>();
        for (int index = 0; index < count; index++)
        {
            using HttpResponseMessage response = await SendAsync(client, method, path, subject: subject);
            statuses.Add(response.StatusCode);
        }

        return [.. statuses];
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string? subject = null,
        string? clientId = null,
        string? authorizedParty = null,
        string? merchantId = null,
        string? remoteIp = null,
        string? forwardedFor = null)
    {
        using HttpRequestMessage request = new(method, path);
        if (subject is not null)
        {
            request.Headers.Add("X-Test-Subject", subject);
        }

        if (clientId is not null)
        {
            request.Headers.Add("X-Test-ClientId", clientId);
        }

        if (authorizedParty is not null)
        {
            request.Headers.Add("X-Test-Azp", authorizedParty);
        }

        if (merchantId is not null)
        {
            request.Headers.Add("X-Test-Merchant", merchantId);
        }

        if (remoteIp is not null)
        {
            request.Headers.Add("X-Remote-IP", remoteIp);
        }

        if (forwardedFor is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed class TestExceptionHandler : IExceptionHandler
    {
        public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
            => ValueTask.FromResult(false);
    }

    private sealed class HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>();
            if (Request.Headers.TryGetValue("X-Test-Subject", out var subject))
            {
                claims.Add(new Claim("sub", subject.ToString()));
            }

            if (Request.Headers.TryGetValue("X-Test-ClientId", out var clientId))
            {
                claims.Add(new Claim("client_id", clientId.ToString()));
            }

            if (Request.Headers.TryGetValue("X-Test-Azp", out var authorizedParty))
            {
                claims.Add(new Claim("azp", authorizedParty.ToString()));
            }

            if (Request.Headers.TryGetValue("X-Test-Merchant", out var merchantId))
            {
                claims.Add(new Claim("merchant_id", merchantId.ToString()));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    private sealed record ObservedMetric(
        string Name,
        long Measurement,
        KeyValuePair<string, object?>[] Tags);
}
