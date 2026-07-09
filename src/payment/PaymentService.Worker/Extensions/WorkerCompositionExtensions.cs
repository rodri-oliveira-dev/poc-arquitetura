using PaymentService.Application;
using PaymentService.Application.Payments.InboxProcessing;
using PaymentService.Application.Payments.Ledger;
using PaymentService.Infrastructure;
using PaymentService.Infrastructure.Ledger;
using PaymentService.Worker.HostedServices;
using PaymentService.Worker.Options;

namespace PaymentService.Worker.Extensions;

public static class WorkerCompositionExtensions
{
    public static IServiceCollection AddPaymentWorkerComposition(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddPaymentApplication();
        services.AddPaymentInfrastructure(configuration, environment);
        services.AddOptions<PaymentInboxWorkerOptions>()
            .Bind(configuration.GetSection(PaymentInboxWorkerOptions.SectionName))
            .Validate(options => options.BatchSize > 0, "PaymentService:InboxWorker:BatchSize deve ser maior que zero.")
            .Validate(options => options.PollingInterval > TimeSpan.Zero, "PaymentService:InboxWorker:PollingInterval deve ser maior que zero.")
            .Validate(options => options.MaxRetryCount >= 0, "PaymentService:InboxWorker:MaxRetryCount nao pode ser negativo.")
            .Validate(options => options.BaseRetryDelay > TimeSpan.Zero, "PaymentService:InboxWorker:BaseRetryDelay deve ser maior que zero.")
            .Validate(options => options.MaxRetryDelay >= options.BaseRetryDelay, "PaymentService:InboxWorker:MaxRetryDelay deve ser maior ou igual ao BaseRetryDelay.")
            .Validate(options => options.ProcessingLeaseTimeout > options.PollingInterval, "PaymentService:InboxWorker:ProcessingLeaseTimeout deve ser maior que PollingInterval.")
            .ValidateOnStart();
        services.AddOptions<PaymentLedgerWorkerOptions>()
            .Bind(configuration.GetSection(PaymentLedgerWorkerOptions.SectionName))
            .Validate(options => options.BatchSize > 0, "PaymentService:LedgerWorker:BatchSize deve ser maior que zero.")
            .Validate(options => options.PollingInterval > TimeSpan.Zero, "PaymentService:LedgerWorker:PollingInterval deve ser maior que zero.")
            .Validate(options => options.MaxRetryCount > 0, "PaymentService:LedgerWorker:MaxRetryCount deve ser maior que zero.")
            .Validate(options => options.BaseRetryDelay > TimeSpan.Zero, "PaymentService:LedgerWorker:BaseRetryDelay deve ser maior que zero.")
            .Validate(options => options.MaxRetryDelay >= options.BaseRetryDelay, "PaymentService:LedgerWorker:MaxRetryDelay deve ser maior ou igual ao BaseRetryDelay.")
            .Validate(options => options.ProcessingLeaseTimeout > options.PollingInterval, "PaymentService:LedgerWorker:ProcessingLeaseTimeout deve ser maior que PollingInterval.")
            .ValidateOnStart();
        services.AddOptions<PaymentLedgerOptions>()
            .Bind(configuration.GetSection(PaymentLedgerOptions.SectionName))
            .Validate(options => options.BaseAddress is not null, "PaymentService:Ledger:BaseAddress nao configurado.")
            .Validate(options => options.Timeout > TimeSpan.Zero, "PaymentService:Ledger:Timeout deve ser maior que zero.")
            .Validate(options => options.Auth.TokenEndpoint is not null, "PaymentService:Ledger:Auth:TokenEndpoint nao configurado.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Auth.ClientId), "PaymentService:Ledger:Auth:ClientId nao configurado.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Auth.ClientSecret), "PaymentService:Ledger:Auth:ClientSecret nao configurado.")
            .Validate(options => string.Equals(options.Auth.Scope, "ledger.write", StringComparison.Ordinal), "PaymentService:Ledger:Auth:Scope deve ser ledger.write.")
            .Validate(options => options.Auth.RefreshSkew > TimeSpan.Zero, "PaymentService:Ledger:Auth:RefreshSkew deve ser maior que zero.")
            .ValidateOnStart();
        services.AddSingleton(sp =>
        {
            var workerOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PaymentInboxWorkerOptions>>().Value;
            return new PaymentInboxProcessingOptions
            {
                MaxRetryCount = workerOptions.MaxRetryCount,
                BaseRetryDelay = workerOptions.BaseRetryDelay,
                MaxRetryDelay = workerOptions.MaxRetryDelay
            };
        });
        services.AddSingleton(sp =>
        {
            var workerOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PaymentLedgerWorkerOptions>>().Value;
            return new PaymentLedgerProcessingOptions
            {
                MaxRetryCount = workerOptions.MaxRetryCount,
                BaseRetryDelay = workerOptions.BaseRetryDelay,
                MaxRetryDelay = workerOptions.MaxRetryDelay
            };
        });
        services.AddPaymentWorkerObservability(configuration);
        services.AddHostedService<PaymentInboxWorkerService>();
        services.AddHostedService<PaymentLedgerWorkerService>();

        return services;
    }
}
