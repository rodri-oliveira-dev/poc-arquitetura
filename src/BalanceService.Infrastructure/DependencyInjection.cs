using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Infrastructure.Messaging.Kafka;
using BalanceService.Infrastructure.Persistence;
using BalanceService.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        // Kafka consumer é opcional no ambiente de testes de integração/locais.
        // Caso contrário, o ValidateOnStart + HostedService impedem a API de subir sem Kafka.
        var kafkaEnabled = configuration.GetValue<bool>("Kafka:Enabled", defaultValue: true);
        if (kafkaEnabled)
        {
            services.AddOptions<KafkaConsumerOptions>()
                .Bind(configuration.GetSection(KafkaConsumerOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers), "Kafka BootstrapServers não configurado.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.GroupId), "Kafka GroupId não configurado.")
                .Validate(o => o.Topics is not null && o.Topics.Count > 0, "Kafka Topics não configurado.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.DeadLetterTopic), "Kafka DeadLetterTopic não configurado.")
                .Validate(o => o.InvalidMessageRetryDelay > TimeSpan.Zero, "Kafka InvalidMessageRetryDelay deve ser maior que zero.")
                .Validate(o => o.ConsumeErrorRetryDelay > TimeSpan.Zero, "Kafka ConsumeErrorRetryDelay deve ser maior que zero.")
                .Validate(o => o.ProcessingErrorRetryDelay > TimeSpan.Zero, "Kafka ProcessingErrorRetryDelay deve ser maior que zero.")
                .ValidateOnStart();

            services.AddSingleton<IKafkaDeadLetterProducer, KafkaDeadLetterProducer>();
            services.AddSingleton<LedgerKafkaMessageProcessor>();
            services.AddHostedService<LedgerEventsConsumer>();
        }

        return services;
    }
}
