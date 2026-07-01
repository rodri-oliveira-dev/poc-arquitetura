using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using TransferService.Api.Extensions;
using TransferService.Api.Observability;

namespace TransferService.IntegrationTests.Api.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddApiObservability_should_bind_options_and_keep_services_when_disabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OpenTelemetryOptions.SectionName}:Enabled"] = "false",
                [$"{OpenTelemetryOptions.SectionName}:ServiceName"] = "transfer-api-tests"
            })
            .Build();
        var services = new ServiceCollection();

        var returned = services.AddApiObservability(configuration);

        Assert.Same(services, returned);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenTelemetryOptions>>().Value;
        Assert.False(options.Enabled);
        Assert.Equal("transfer-api-tests", options.ServiceName);
    }

    [Fact]
    public void Public_extensions_should_validate_required_arguments()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddApiSwagger(null!));
        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddApiObservability(null!, configuration));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddApiObservability(null!));
    }
}
