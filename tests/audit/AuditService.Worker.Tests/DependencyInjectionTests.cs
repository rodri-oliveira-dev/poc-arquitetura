using AuditService.Application.FunctionalAuditing.Ingestion;
using AuditService.Worker.HostedServices;
using AuditService.Worker.Messaging.Kafka;
using AuditService.Worker.Messaging.Kafka.Configuration;
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
    public void AddAuditWorkerComposition_should_register_kafka_consumer_when_enabled()
    {
        var services = new ServiceCollection();

        DependencyInjection.AddAuditWorkerComposition(
            services,
            CreateConfiguration(new Dictionary<string, string?>
            {
                ["AuditService:Worker:Enabled"] = "true",
                ["Kafka:AuditRecordRequestedConsumer:Enabled"] = "true",
                ["Kafka:AuditRecordRequestedConsumer:BootstrapServers"] = "localhost:9092"
            }),
            CreateEnvironment());

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAuditRecordRequestedProcessor));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAuditKafkaConsumerFactory));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(AuditRecordRequestedConsumerService));
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

    [Fact]
    public void AuditRecordRequestedConsumerOptions_should_use_safe_defaults()
    {
        using ServiceProvider provider = CreateProvider();

        AuditRecordRequestedConsumerOptions options = provider.GetRequiredService<IOptions<AuditRecordRequestedConsumerOptions>>().Value;

        Assert.False(options.Enabled);
        Assert.Equal("audit.record.requested", options.Topic);
        Assert.False(options.EnableAutoCommit);
        Assert.False(options.EnableAutoOffsetStore);
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
