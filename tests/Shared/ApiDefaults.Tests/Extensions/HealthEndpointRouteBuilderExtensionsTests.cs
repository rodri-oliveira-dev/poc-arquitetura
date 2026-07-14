using System.Net;
using System.Text.Json;

using ApiDefaults.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace ApiDefaults.Tests.Extensions;

public sealed class HealthEndpointRouteBuilderExtensionsTests
{
    [Fact]
    public async Task MapApiHealthEndpoints_ShouldMapHealthAsAnonymousPlainTextGet()
    {
        await using WebApplication app = BuildHealthApplication((_, _) => Task.FromResult(true));
        await app.StartAsync(TestContext.Current.CancellationToken);
        HttpClient client = app.GetTestClient();

        using HttpResponseMessage response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        using HttpResponseMessage postResponse = await client.PostAsync("/health", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, postResponse.StatusCode);
    }

    [Fact]
    public async Task MapApiHealthEndpoints_WhenReadinessIsHealthy_ShouldReturnReadyPayload()
    {
        await using WebApplication app = BuildHealthApplication((_, _) => Task.FromResult(true));
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpResponseMessage response = await app.GetTestClient().GetAsync("/ready", TestContext.Current.CancellationToken);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("ready", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("ok", json.RootElement.GetProperty("checks").GetProperty("db").GetString());
        Assert.DoesNotContain("exception", json.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MapApiHealthEndpoints_WhenReadinessIsUnhealthy_ShouldReturnServiceUnavailableWithoutDetails()
    {
        await using WebApplication app = BuildHealthApplication((_, _) => Task.FromResult(false));
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpResponseMessage response = await app.GetTestClient().GetAsync("/ready", TestContext.Current.CancellationToken);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("not_ready", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("unavailable", json.RootElement.GetProperty("checks").GetProperty("db").GetString());
        Assert.DoesNotContain("stack", json.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MapApiHealthEndpoints_WhenCustomReadinessHasMixedChecks_ShouldApplyPredicateToAllValues()
    {
        var state = new[] { "db", "cache" };
        WebApplicationBuilder builder = CreateBuilder();
        await using WebApplication app = builder.Build();

        app.MapApiHealthEndpoints(
            static (_, checks, _) => Task.FromResult<IReadOnlyDictionary<string, string>>(
                checks.ToDictionary(check => check, check => check == "db" ? "ok" : "degraded")),
            state,
            "Readiness de teste.");

        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpResponseMessage response = await app.GetTestClient().GetAsync("/ready", TestContext.Current.CancellationToken);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("ok", json.RootElement.GetProperty("checks").GetProperty("db").GetString());
        Assert.Equal("degraded", json.RootElement.GetProperty("checks").GetProperty("cache").GetString());
    }

    [Fact]
    public void MapApiHealthEndpoints_WhenArgumentsAreInvalid_ShouldThrow()
    {
        WebApplicationBuilder builder = CreateBuilder();
        using WebApplication app = builder.Build();

        Assert.Throws<ArgumentNullException>("app", () =>
            HealthEndpointRouteBuilderExtensions.MapApiHealthEndpoints(null!, (_, _) => Task.FromResult(true), "ready"));
        Assert.Throws<ArgumentNullException>("canConnectToDatabase", () =>
            app.MapApiHealthEndpoints(null!, "ready"));
        Assert.Throws<ArgumentException>("readinessDescription", () =>
            app.MapApiHealthEndpoints((_, _) => Task.FromResult(true), " "));
        Assert.Throws<ArgumentNullException>("readinessChecks", () =>
            app.MapApiHealthEndpoints(null!, "state", "ready"));
    }

    private static WebApplication BuildHealthApplication(Func<IServiceProvider, CancellationToken, Task<bool>> canConnect)
    {
        WebApplicationBuilder builder = CreateBuilder();
        WebApplication app = builder.Build();
        app.MapApiHealthEndpoints(canConnect, "Readiness de teste.");
        return app;
    }

    private static WebApplicationBuilder CreateBuilder()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Test"
        });
        builder.WebHost.UseTestServer();
        return builder;
    }
}
