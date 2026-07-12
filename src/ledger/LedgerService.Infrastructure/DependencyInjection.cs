using LedgerService.Application.Abstractions.Messaging;
using LedgerService.Application.Idempotency;
using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Observability;
using LedgerService.Infrastructure.Persistence;
using LedgerService.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LedgerService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Fachada de compatibilidade para consumidores antigos da Infrastructure.
        // Composition roots novos devem usar AddLedgerApiInfrastructure ou os metodos
        // explicitos de Worker para evitar HostedServices acidentais em processos HTTP.
        return services.AddLedgerApiInfrastructure(configuration, environment);
    }

    public static IServiceCollection AddLedgerApiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddLedgerInfrastructureCommon()
            .AddLedgerPersistence(configuration)
            .AddLedgerRepositories();

        return services;
    }

    public static IServiceCollection AddLedgerInfrastructureCommon(this IServiceCollection services)
    {
        services.AddSingleton<OutboxMetrics>();

        return services;
    }

    public static IServiceCollection AddLedgerPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' nao foi configurada.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "ledger")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        return services;
    }

    public static IServiceCollection AddLedgerRepositories(this IServiceCollection services)
    {
        services.AddScoped<ILedgerEntryRepository, LedgerEntryRepository>();
        services.AddScoped<IEstornoLancamentoRepository, EstornoLancamentoRepository>();
        services.AddScoped<IReprocessamentoLancamentosRepository, ReprocessamentoLancamentosRepository>();
        services.AddScoped<IIdempotencyRecordRepository, IdempotencyRecordRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();

        return services;
    }

}
