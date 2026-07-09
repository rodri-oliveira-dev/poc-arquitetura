using PaymentService.Application;
using PaymentService.Application.Payments.InboxProcessing;
using PaymentService.Infrastructure;
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
        services.AddPaymentWorkerObservability(configuration);
        services.AddHostedService<PaymentInboxWorkerService>();

        return services;
    }
}
