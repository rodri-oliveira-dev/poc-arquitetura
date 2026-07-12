using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using PaymentService.Worker.Extensions;
using PaymentService.Worker.HostedServices;
using PaymentService.Worker.Observability;

namespace PaymentService.UnitTests.Worker;

public sealed class PaymentWorkerCompositionTests
{
    [Fact]
    public void AddPaymentWorkerComposition_should_register_worker_services_and_safe_observability_defaults()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(("Observability:OpenTelemetry:Enabled", "false"));

        services.AddPaymentWorkerComposition(configuration, new TestHostEnvironment());

        using var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>().ToList();

        Assert.Contains(hostedServices, service => service is PaymentInboxWorkerService);
        Assert.Contains(hostedServices, service => service is PaymentLedgerWorkerService);
        Assert.NotNull(provider.GetRequiredService<PaymentInboxWorkerMetrics>());
        Assert.NotNull(provider.GetRequiredService<PaymentLedgerWorkerMetrics>());
    }

    [Fact]
    public void AddPaymentWorkerObservability_should_accept_enabled_otlp_configuration()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            ("Observability:OpenTelemetry:Enabled", "true"),
            ("Observability:OpenTelemetry:ServiceName", "PaymentService.Worker.Tests"),
            ("Observability:OpenTelemetry:UseConsoleExporter", "false"),
            ("Observability:OpenTelemetry:OtlpEndpoint", "http://localhost:4317"));

        services.AddPaymentWorkerObservability(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<PaymentInboxWorkerMetrics>());
        Assert.NotNull(provider.GetRequiredService<PaymentLedgerWorkerMetrics>());
    }

    [Fact]
    public void Payment_worker_metrics_should_record_low_cardinality_outcomes()
    {
        using var inboxMetrics = new PaymentInboxWorkerMetrics();
        using var ledgerMetrics = new PaymentLedgerWorkerMetrics();

        inboxMetrics.RecordClaim(count: 2, recoveredLeaseCount: 1);
        inboxMetrics.RecordProcess("retry_scheduled", elapsedMilliseconds: 12);
        inboxMetrics.RecordProcess("dead_letter", elapsedMilliseconds: 13);
        inboxMetrics.RecordProcess("regressive_ignored", elapsedMilliseconds: 14);
        inboxMetrics.RecordProcess("idempotent", elapsedMilliseconds: 15);
        inboxMetrics.RecordFailure("unexpected_message_error");
        inboxMetrics.SetBacklog(-10);

        ledgerMetrics.RecordBatch(
            new PaymentLedgerWorkerBatchMetrics(
                Claimed: 3,
                Completed: 1,
                RetryScheduled: 1,
                FailedDefinitive: 1,
                DeadLettered: 0),
            elapsedMilliseconds: 20);
        ledgerMetrics.RecordFailure("unexpected_poll_error");
    }

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=15432;Database=appdb;Username=payment_app_user;Password=placeholder",
            ["PaymentGateway:Provider"] = "Fake",
            ["PaymentGateway:Fake:Scenario"] = "Success",
            ["PaymentGateway:Stripe:WebhookSignatureTolerance"] = "00:05:00",
            ["PaymentService:InboxWorker:PollingInterval"] = "00:00:02",
            ["PaymentService:InboxWorker:BatchSize"] = "20",
            ["PaymentService:InboxWorker:MaxRetryCount"] = "5",
            ["PaymentService:InboxWorker:BaseRetryDelay"] = "00:00:05",
            ["PaymentService:InboxWorker:MaxRetryDelay"] = "00:05:00",
            ["PaymentService:InboxWorker:ProcessingLeaseTimeout"] = "00:01:00",
            ["PaymentService:LedgerWorker:PollingInterval"] = "00:00:02",
            ["PaymentService:LedgerWorker:BatchSize"] = "20",
            ["PaymentService:LedgerWorker:MaxRetryCount"] = "5",
            ["PaymentService:LedgerWorker:BaseRetryDelay"] = "00:00:05",
            ["PaymentService:LedgerWorker:MaxRetryDelay"] = "00:05:00",
            ["PaymentService:LedgerWorker:ProcessingLeaseTimeout"] = "00:01:00",
            ["PaymentService:Ledger:BaseAddress"] = "http://localhost:5001",
            ["PaymentService:Ledger:Timeout"] = "00:00:10",
            ["PaymentService:Ledger:Auth:TokenEndpoint"] = "http://localhost:8080/realms/poc/protocol/openid-connect/token",
            ["PaymentService:Ledger:Auth:ClientId"] = "poc-automation",
            ["PaymentService:Ledger:Auth:ClientSecret"] = "placeholder",
            ["PaymentService:Ledger:Auth:Scope"] = "ledger.write",
            ["PaymentService:Ledger:Auth:RefreshSkew"] = "00:01:00",
            ["Observability:OpenTelemetry:Enabled"] = "false",
            ["Observability:OpenTelemetry:ServiceName"] = "PaymentService.Worker"
        };

        foreach (var (key, value) in overrides)
        {
            values[key] = value;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName
        {
            get; set;
        } = Environments.Development;

        public string ApplicationName
        {
            get; set;
        } = "PaymentService.Worker.Tests";

        public string ContentRootPath
        {
            get; set;
        } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider
        {
            get; set;
        } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
