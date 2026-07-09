using PaymentService.Application;
using PaymentService.Infrastructure;

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

        return services;
    }
}
