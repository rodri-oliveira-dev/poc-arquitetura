using ApplicationDefaults.Behaviors;

using BalanceService.Application.Balances.Replay;
using BalanceService.Application.Common.Observability;
using BalanceService.Application.Contracts.Events;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

namespace BalanceService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<BalanceDomainMetrics>();
        services.AddSingleton<IEventContractSchemaCatalog, EmbeddedEventContractSchemaCatalog>();
        services.AddSingleton<IEventContractValidator, JsonSchemaEventContractValidator>();
        services.AddScoped<EventReplayMessageEvaluator>();

        return services;
    }
}
