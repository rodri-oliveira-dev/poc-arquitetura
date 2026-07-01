using AuditService.Api.Tests.Security;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AuditService.Api.Tests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Health_should_return_200_ok()
    {
        using var factory = new AuditApiFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        HttpResponseMessage response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Ready_should_return_200_ok_without_external_dependencies()
    {
        using var factory = new AuditApiFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        HttpResponseMessage response = await client.GetAsync("/ready", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"status\":\"ready\"", body);
        Assert.Contains("\"self\":\"ok\"", body);
    }

    private sealed class AuditApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = TestJwtTokenFactory.KeycloakIssuer,
                    ["Jwt:Audience"] = TestJwtTokenFactory.AuditAudience,
                    ["Jwt:JwksUrl"] = "https://localhost/jwks.json"
                });
            });
        }
    }
}
