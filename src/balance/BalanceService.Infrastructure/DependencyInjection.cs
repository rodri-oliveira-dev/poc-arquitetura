using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Balances.Replay;
using BalanceService.Infrastructure.Persistence;
using BalanceService.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BalanceService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return services.AddBalanceApiInfrastructure(configuration, environment);
    }

    public static IServiceCollection AddBalanceApiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddBalanceInfrastructureCommon()
            .AddBalancePersistence(configuration)
            .AddBalanceRepositories();

        return services;
    }

    public static IServiceCollection AddBalanceInfrastructureCommon(this IServiceCollection services)
    {
        return services;
    }

    public static IServiceCollection AddBalancePersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' nao foi configurada.");

        services.AddDbContext<BalanceDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "balance")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<BalanceDbContext>());

        return services;
    }

    public static IServiceCollection AddBalanceRepositories(this IServiceCollection services)
    {
        services.AddScoped<IDailyBalanceRepository, DailyBalanceRepository>();
        services.AddScoped<IDailyBalanceReadRepository, DailyBalanceReadRepository>();
        services.AddScoped<IProcessedEventRepository, ProcessedEventRepository>();
        services.AddScoped<IFilteredEventReplaySource, OutboxFilteredEventReplaySource>();

        return services;
    }

}
