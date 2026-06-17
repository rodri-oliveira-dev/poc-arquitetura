using Microsoft.Extensions.DependencyInjection;
using TransferService.Application;

namespace TransferService.UnitTests.Composition;

public sealed class TransferApplicationCompositionTests
{
    [Fact]
    public void AddTransferApplication_should_keep_minimal_composition_root()
    {
        ServiceCollection services = [];

        IServiceCollection result = services.AddTransferApplication();

        Assert.Same(services, result);
    }
}
