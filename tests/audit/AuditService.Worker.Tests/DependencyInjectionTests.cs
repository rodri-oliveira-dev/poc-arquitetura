using AuditService.Application.FunctionalAuditing.Ingestion;
using AuditService.Worker.HostedServices;
using AuditService.Worker.Options;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AuditService.Worker.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddAuditWorkerComposition_should_register_audit_context_dependencies()
    {
        var services = new ServiceCollection();

        IServiceCollection result = DependencyInjection.AddAuditWorkerComposition(services, CreateConfiguration(), CreateEnvironment());

        Assert.Same(services, result);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAuditRecordIngestionService));
        Assert.Contains(services, descriptor => descriptor.ServiceType.FullName == "AuditService.Infrastructure.Persistence.AuditDbContext");
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(AuditWorkerPlaceholderService));
    }

    [Fact]
    public void AuditWorkerOptions_should_use_safe_defaults()
    {
        using ServiceProvider provider = CreateProvider();

        AuditWorkerOptions options = provider.GetRequiredService<IOptions<AuditWorkerOptions>>().Value;

        Assert.False(options.Enabled);
        Assert.Equal(TimeSpan.FromMinutes(5), options.IdleDelay);
    }

    [Fact]
    public void AuditWorkerOptions_should_reject_non_positive_idle_delay()
    {
        using ServiceProvider provider = CreateProvider(new Dictionary<string, string?>
        {
            ["AuditService:Worker:IdleDelay"] = "00:00:00"
        });

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<AuditWorkerOptions>>().Value);

        Assert.Contains(exception.Failures, failure => failure.Contains("IdleDelay", StringComparison.Ordinal));
    }

    private static ServiceProvider CreateProvider(Dictionary<string, string?>? overrides = null)
    {
        var services = new ServiceCollection();
        DependencyInjection.AddAuditWorkerComposition(services, CreateConfiguration(overrides), CreateEnvironment());
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=audit-worker-tests;Username=app;Password=app"
        };

        if (overrides is not null)
        {
            foreach (KeyValuePair<string, string?> item in overrides)
            {
                values[item.Key] = item.Value;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static TestHostEnvironment CreateEnvironment()
    {
        return new TestHostEnvironment();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";

        public string ApplicationName { get; set; } = "AuditService.Worker.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider
        {
            get; set;
        } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
