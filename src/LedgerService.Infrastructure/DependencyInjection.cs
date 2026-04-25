using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Persistence;
using LedgerService.Infrastructure.Repositories;
using LedgerService.Infrastructure.Messaging.Kafka;
using LedgerService.Infrastructure.Outbox;
using Microsoft.Extensions.Hosting;

namespace LedgerService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
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
                .Validate(o => IsLocalEnvironment(environment) || !KafkaClientConfigExtensions.IsPlaintext(o), "Kafka PLAINTEXT é permitido apenas em Development/Local.")
                .ValidateOnStart();

            services.AddSingleton<IOutboxEventProducer, OutboxKafkaProducer>();

            services.AddOptions<OutboxPublisherOptions>()
                .Bind(configuration.GetSection(OutboxPublisherOptions.SectionName))
                .ValidateOnStart();

            services.AddHostedService<OutboxKafkaPublisherService>();
        }

        return services;
    }

    private static bool IsLocalEnvironment(IHostEnvironment environment)
        => environment.IsDevelopment()
            || environment.IsEnvironment("Local")
            || environment.IsEnvironment("Test");
}
