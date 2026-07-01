using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

using Moq;

using InfrastructureDependencyInjection = AuditService.Infrastructure.DependencyInjection;

namespace AuditService.Infrastructure.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddAuditInfrastructure_should_keep_service_collection_available_without_external_dependencies()
    {
        var services = new ServiceCollection();

        IServiceCollection result = InfrastructureDependencyInjection.AddAuditInfrastructure(
            services,
            Mock.Of<IConfiguration>(),
            new TestHostEnvironment());

        Assert.Same(services, result);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "AuditService.Infrastructure.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
