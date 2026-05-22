using Auth.Api.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Auth.UnitTests.Swagger;

public sealed class SwaggerExposurePolicyTests
{
    [Fact]
    public void IsSwaggerEnabled_should_enable_by_default_in_development()
    {
        var enabled = WebApplicationExtensions.IsSwaggerEnabled(
            CreateEnvironment(Environments.Development),
            CreateConfiguration(swaggerEnabled: null));
        Assert.True(enabled);
    }

    [Fact]
    public void IsSwaggerEnabled_should_disable_by_default_outside_development()
    {
        var enabled = WebApplicationExtensions.IsSwaggerEnabled(
            CreateEnvironment(Environments.Production),
            CreateConfiguration(swaggerEnabled: null));
        Assert.False(enabled);
    }

    [Fact]
    public void IsSwaggerEnabled_should_allow_explicit_enable_outside_development()
    {
        var enabled = WebApplicationExtensions.IsSwaggerEnabled(
            CreateEnvironment(Environments.Staging),
            CreateConfiguration(swaggerEnabled: true));
        Assert.True(enabled);
    }

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        return environment.Object;
    }

    private static IConfiguration CreateConfiguration(bool? swaggerEnabled)
    {
        var values = new Dictionary<string, string?>();
        if (swaggerEnabled.HasValue)
            values["Swagger:Enabled"] = swaggerEnabled.Value.ToString();

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
