using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Infrastructure.Persistence;
using BalanceService.Infrastructure.Persistence.Repositories;
using BalanceService.Infrastructure.Messaging.Kafka;
using Microsoft.Extensions.Options;


namespace BalanceService.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não foi configurada.");

        services.AddDbContext<BalanceDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<BalanceDbContext>());

        services.AddScoped<IDailyBalanceRepository, DailyBalanceRepository>();
        services.AddScoped<IDailyBalanceReadRepository, DailyBalanceReadRepository>();
        services.AddScoped<IProcessedEventRepository, ProcessedEventRepository>();

        services.AddOptions<KafkaConsumerOptions>()
            .Bind(configuration.GetSection(KafkaConsumerOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers), "Kafka BootstrapServers não configurado.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.GroupId), "Kafka GroupId não configurado.")
            .Validate(o => o.Topics is not null && o.Topics.Count > 0, "Kafka Topics não configurado.")
            .ValidateOnStart();

        services.AddHostedService<LedgerEventsConsumer>();
        return services;
    }
}
