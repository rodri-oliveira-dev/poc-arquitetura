using AuditService.Application.Abstractions.Persistence;
using AuditService.Infrastructure.Persistence;
using AuditService.Infrastructure.Persistence.Queries;
using AuditService.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AuditService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuditInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        return services.AddAuditPersistence(configuration);
    }

    public static IServiceCollection AddAuditPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' nao foi configurada.");

        services.AddDbContext<AuditDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .MigrationsHistoryTable("__EFMigrationsHistory", "audit")
                    .ConfigureDataSource(dataSourceBuilder => dataSourceBuilder.EnableDynamicJson())));

        services.AddScoped<IFunctionalAuditRecordRepository, FunctionalAuditRecordRepository>();
        services.AddScoped<IFunctionalAuditRecordQueryService, FunctionalAuditRecordQueryService>();

        return services;
    }
}
