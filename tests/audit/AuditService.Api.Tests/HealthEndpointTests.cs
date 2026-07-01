using AuditService.Api.Tests.Security;
using AuditService.Infrastructure.Persistence;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    public async Task Ready_should_return_200_ok_when_database_is_available()
    {
        using var factory = new AuditApiFactory(useInMemoryDatabase: true);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        HttpResponseMessage response = await client.GetAsync("/ready", TestContext.Current.CancellationToken);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(
            response.StatusCode == System.Net.HttpStatusCode.OK,
            $"Expected 200 OK, got {(int)response.StatusCode}. Body: {body}");
        Assert.Contains("\"status\":\"ready\"", body);
        Assert.Contains("\"db\":\"ok\"", body);
    }

    [Fact]
    public async Task Ready_should_return_503_when_database_is_unavailable()
    {
        using var factory = new AuditApiFactory(useInMemoryDatabase: false);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        HttpResponseMessage response = await client.GetAsync("/ready", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"status\":\"not_ready\"", body);
        Assert.Contains("\"db\":\"unavailable\"", body);
    }

    private sealed class AuditApiFactory(bool useInMemoryDatabase = false) : WebApplicationFactory<Program>
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
                    ["Jwt:JwksUrl"] = "https://localhost/jwks.json",
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing;Timeout=1"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                if (!useInMemoryDatabase)
                    return;

                services.RemoveAll<AuditDbContext>();
                services.RemoveAll<DbContextOptions<AuditDbContext>>();

                services.AddDbContext<AuditDbContext>(options =>
                    options
                        .UseNpgsql(
                            "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing;Timeout=1")
                        .ReplaceService<IDatabaseCreator, AvailableDatabaseCreator>());
            });
        }

        private sealed class AvailableDatabaseCreator : IDatabaseCreator
        {
            public bool EnsureDeleted()
                => throw new NotSupportedException();

            public Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public bool EnsureCreated()
                => throw new NotSupportedException();

            public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public bool CanConnect()
                => true;

            public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(true);
        }
    }
}
