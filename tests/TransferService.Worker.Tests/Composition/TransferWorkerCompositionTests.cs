using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TransferService.Worker.Extensions;

namespace TransferService.Worker.Tests.Composition;

public sealed class TransferWorkerCompositionTests
{
    [Fact]
    public void AddTransferWorkerComposition_should_keep_minimal_composition_root()
    {
        ServiceCollection services = [];
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=transfer-tests;Username=app;Password=app"
        });

        IServiceCollection result = services.AddTransferWorkerComposition(builder.Configuration, builder.Environment);

        Assert.Same(services, result);
    }
}
