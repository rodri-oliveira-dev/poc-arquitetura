using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TransferService.Api.Extensions;

namespace TransferService.IntegrationTests.Composition;

public sealed class TransferApiCompositionTests
{
    [Fact]
    public void AddTransferApiComposition_should_keep_minimal_composition_root()
    {
        ServiceCollection services = [];
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=transfer-tests;Username=app;Password=app"
        });

        IServiceCollection result = services.AddTransferApiComposition(builder.Configuration, builder.Environment);

        Assert.Same(services, result);
    }
}
