using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using TransferService.Application.Abstractions.Messaging;
using TransferService.Application.Abstractions.Persistence;
using TransferService.Infrastructure.Messaging.Kafka;
using TransferService.Infrastructure.Persistence;
using TransferService.Infrastructure.Persistence.Outbox;
using TransferService.Infrastructure.Persistence.Repositories;

namespace TransferService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTransferInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services
            .AddTransferPersistence(configuration)
            .AddTransferRepositories()
            .AddTransferMessaging(configuration);

        return services;
    }

    public static IServiceCollection AddTransferPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' nao foi configurada.");

        services.AddDbContext<TransferServiceDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "transfer")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TransferServiceDbContext>());

        return services;
    }

    public static IServiceCollection AddTransferRepositories(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ITransferenciaSagaRepository, TransferenciaSagaRepository>();
        services.AddScoped<ITransferenciaIdempotencyService, TransferenciaIdempotencyService>();
        services.AddScoped<ITransferenciaOutboxWriter, TransferenciaOutboxWriter>();

        return services;
    }

    public static IServiceCollection AddTransferMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<TransferenciaKafkaTopicOptions>(
            configuration.GetSection(TransferenciaKafkaTopicOptions.SectionName));
        services.AddSingleton<TransferenciaSagaKafkaMetadataMapper>();

        return services;
    }
}
