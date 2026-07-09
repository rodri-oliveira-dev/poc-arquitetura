using ApplicationDefaults.Behaviors;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using PaymentService.Application.Abstractions.Time;

namespace PaymentService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
