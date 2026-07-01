using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;

using InfrastructureDependencyInjection = AuditService.Infrastructure.DependencyInjection;

namespace AuditService.Infrastructure.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddAuditInfrastructure_should_keep_service_collection_available_without_external_dependencies()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new TestConfiguration(
            "Host=127.0.0.1;Database=audit_tests;Username=test");

        IServiceCollection result = InfrastructureDependencyInjection.AddAuditInfrastructure(
            services,
            configuration,
            new TestHostEnvironment());

        Assert.Same(services, result);
        Assert.Contains(services, descriptor => descriptor.ServiceType.FullName == "AuditService.Infrastructure.Persistence.AuditDbContext");
    }

    private sealed class TestConfiguration(string connectionString) : IConfiguration
    {
        public string? this[string key]
        {
            get => key == "ConnectionStrings:DefaultConnection" ? connectionString : null;
            set => throw new NotSupportedException();
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];

        public IChangeToken GetReloadToken() => NullChangeToken.Singleton;

        public IConfigurationSection GetSection(string key)
            => key == "ConnectionStrings"
                ? new TestConfigurationSection(key, connectionString)
                : new TestConfigurationSection(key, null);
    }

    private sealed class TestConfigurationSection(string key, string? connectionString) : IConfigurationSection
    {
        private readonly string? connectionString = connectionString;

        public string? this[string childKey]
        {
            get => Key == "ConnectionStrings" && childKey == "DefaultConnection" ? connectionString : null;
            set => throw new NotSupportedException();
        }

        public string Key
        {
            get;
        } = key;

        public string Path => Key;

        public string? Value
        {
            get; set;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];

        public IChangeToken GetReloadToken() => NullChangeToken.Singleton;

        public IConfigurationSection GetSection(string childKey) => new TestConfigurationSection(childKey, null);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "AuditService.Infrastructure.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
