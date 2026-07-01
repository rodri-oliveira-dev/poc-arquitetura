using ApplicationDefaults.Behaviors;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using TransferService.Application.Abstractions.Time;

namespace TransferService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTransferApplication(this IServiceCollection services)
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
