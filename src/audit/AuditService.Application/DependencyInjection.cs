using ApplicationDefaults.Behaviors;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

namespace AuditService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAuditApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
