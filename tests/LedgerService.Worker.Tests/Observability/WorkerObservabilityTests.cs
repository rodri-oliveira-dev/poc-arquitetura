using LedgerService.Worker.HostedServices;
using LedgerService.Worker.Extensions;
using LedgerService.Worker.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace LedgerService.Worker.Tests.Observability;

public sealed class WorkerObservabilityTests
{
    [Fact]
    public void AddWorkerObservability_should_register_lifecycle_hosted_service()
    {
        var services = new ServiceCollection();

        services.AddWorkerObservability(CreateConfiguration());

        using var provider = services
            .AddLogging()
            .BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>();
        Assert.Single(hostedServices, x => x is WorkerLifecycleLogService);
    }

    [Fact]
    public void AddWorkerObservability_should_validate_service_name_when_otel_is_enabled()
    {
        var services = new ServiceCollection();

        services.AddWorkerObservability(CreateConfiguration(new Dictionary<string, string?>
        {
            ["Observability:OpenTelemetry:Enabled"] = "true",
            ["Observability:OpenTelemetry:ServiceName"] = ""
        }));

        using var provider = services.BuildServiceProvider();
        var act = () => provider.GetRequiredService<IOptions<OpenTelemetryOptions>>().Value;
        var ex = Assert.Throws<OptionsValidationException>(act);
        Assert.Matches("^" + System.Text.RegularExpressions.Regex.Escape("*Observability OpenTelemetry ServiceName*").Replace("\\*", ".*") + "$", ex.Message);
    }

    [Fact]
    public async Task WorkerLifecycleLogService_should_start_and_stop_without_side_effects()
    {
        var logger = new Mock<ILogger<WorkerLifecycleLogService>>();
        var sut = new WorkerLifecycleLogService("LedgerService.Worker", logger.Object);

        await sut.StartAsync(CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);
        Assert.Equal(2, logger.Invocations.Count);
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Observability:OpenTelemetry:Enabled"] = "false",
            ["Observability:OpenTelemetry:ServiceName"] = "LedgerService.Worker"
        };

        if (overrides is not null)
        {
            foreach (var item in overrides)
                values[item.Key] = item.Value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
