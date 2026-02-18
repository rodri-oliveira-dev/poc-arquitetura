using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Balances.Commands;
using BalanceService.Application.Balances.Queries;
using BalanceService.Application.Balances.Services;

namespace BalanceService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ApplyLedgerEntryCreatedHandler>();

        // Queries (Balance read model)
        services.AddScoped<IDailyBalanceService, DailyBalanceService>();
        services.AddScoped<IPeriodBalanceService, PeriodBalanceService>();


        return services;
    }
}
