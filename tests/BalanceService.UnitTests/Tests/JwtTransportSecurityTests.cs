using BalanceService.Api.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace BalanceService.UnitTests.Tests;

public sealed class JwtTransportSecurityTests
{
    [Fact]
    public void AddApiJwtAuth_should_allow_http_jwks_and_disabled_https_metadata_in_development()
    {
        var services = new ServiceCollection();

        services.AddApiJwtAuth(CreateConfiguration("http://localhost/jwks.json", requireHttpsMetadata: false), CreateEnvironment(Environments.Development));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        options.RequireHttpsMetadata.Should().BeFalse();
    }

    [Fact]
    public void AddApiJwtAuth_should_reject_disabled_https_metadata_outside_local_environments()
    {
        var services = new ServiceCollection();

        var act = () => services.AddApiJwtAuth(CreateConfiguration("https://auth-api/jwks.json", requireHttpsMetadata: false), CreateEnvironment(Environments.Production));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*RequireHttpsMetadata=false*Development/Local*");
    }

    [Fact]
    public void AddApiJwtAuth_should_reject_http_jwks_outside_local_environments()
    {
        var services = new ServiceCollection();

        var act = () => services.AddApiJwtAuth(CreateConfiguration("http://auth-api/jwks.json", requireHttpsMetadata: true), CreateEnvironment(Environments.Production));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JwksUrl*HTTPS*Development/Local*");
    }

    private static IConfiguration CreateConfiguration(string jwksUrl, bool requireHttpsMetadata)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "https://auth-api",
                ["Jwt:Audience"] = "balance-api",
                ["Jwt:JwksUrl"] = jwksUrl,
                ["Jwt:RequireHttpsMetadata"] = requireHttpsMetadata.ToString()
            })
            .Build();
    }

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        return environment.Object;
    }
}
