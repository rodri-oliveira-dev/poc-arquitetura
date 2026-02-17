using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Persistence;
using LedgerService.Infrastructure.Repositories;
using LedgerService.Infrastructure.Messaging.Kafka;
using LedgerService.Infrastructure.Outbox;

namespace LedgerService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não foi configurada.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<ILedgerEntryRepository, LedgerEntryRepository>();
        services.AddScoped<IIdempotencyRecordRepository, IdempotencyRecordRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();

        // Outbox/Kafka é opcional no ambiente de testes de integração/locais.
        // Caso contrário, o ValidateOnStart + HostedService impedem a API de subir sem Kafka.
        var kafkaEnabled = configuration.GetValue<bool>("Kafka:Enabled", defaultValue: true);
        if (kafkaEnabled)
        {
            services.AddOptions<KafkaProducerOptions>()
                .Bind(configuration.GetSection(KafkaProducerOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers), "Kafka BootstrapServers não configurado.")
                .ValidateOnStart();

            services.AddSingleton<IOutboxEventProducer, OutboxKafkaProducer>();

            services.AddOptions<OutboxPublisherOptions>()
                .Bind(configuration.GetSection(OutboxPublisherOptions.SectionName))
                .ValidateOnStart();

            services.AddHostedService<OutboxKafkaPublisherService>();
        }

        return services;
    }
}
