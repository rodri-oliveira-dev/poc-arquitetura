using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Common.Behaviors;
using BalanceService.Application.Balances.Commands;
using BalanceService.Application.Balances.Services;

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

        services.AddSingleton<IClock, SystemClock>();

        // Queries (Balance read model)
        services.AddScoped<IDailyBalanceService, DailyBalanceService>();
        services.AddScoped<IPeriodBalanceService, PeriodBalanceService>();


        return services;
    }
}
