using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Infrastructure.Messaging.Kafka;
using BalanceService.Infrastructure.Observability;
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
        services
            .AddBalanceApiInfrastructure(configuration, environment)
            .AddBalanceLedgerEventsWorker(configuration);

        return services;
    }

    public static IServiceCollection AddBalanceApiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddBalanceInfrastructureCommon()
            .AddBalancePersistence(configuration)
            .AddBalanceRepositories()
            .AddBalanceKafkaConsumer(configuration, environment);

        return services;
    }

    public static IServiceCollection AddBalanceInfrastructureCommon(this IServiceCollection services)
    {
        services.AddSingleton<KafkaMessagingMetrics>();

        return services;
    }

    public static IServiceCollection AddBalancePersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' nao foi configurada.");

        services.AddDbContext<BalanceDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<BalanceDbContext>());

        return services;
    }

    public static IServiceCollection AddBalanceRepositories(this IServiceCollection services)
    {
        services.AddScoped<IDailyBalanceRepository, DailyBalanceRepository>();
        services.AddScoped<IDailyBalanceReadRepository, DailyBalanceReadRepository>();
        services.AddScoped<IProcessedEventRepository, ProcessedEventRepository>();

        return services;
    }

    public static IServiceCollection AddBalanceKafkaConsumer(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Kafka consumer e opcional no ambiente de testes de integracao/locais.
        // Caso contrario, o ValidateOnStart + HostedService impedem a API de subir sem Kafka.
        var kafkaEnabled = configuration.GetValue<bool>("Kafka:Enabled", defaultValue: true);
        if (!kafkaEnabled)
        {
            return services;
        }

        services.AddOptions<KafkaConsumerOptions>()
            .Bind(configuration.GetSection(KafkaConsumerOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers), "Kafka BootstrapServers nao configurado.")
            .Validate(o => IsLocalEnvironment(environment) || !KafkaClientConfigExtensions.IsPlaintext(o), "Kafka PLAINTEXT e permitido apenas em Development/Local.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.GroupId), "Kafka GroupId nao configurado.")
            .Validate(o => o.Topics is not null && o.Topics.Count > 0, "Kafka Topics nao configurado.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.DeadLetterTopic), "Kafka DeadLetterTopic nao configurado.")
            .Validate(o => o.InvalidMessageRetryDelay > TimeSpan.Zero, "Kafka InvalidMessageRetryDelay deve ser maior que zero.")
            .Validate(o => o.ConsumeErrorRetryDelay > TimeSpan.Zero, "Kafka ConsumeErrorRetryDelay deve ser maior que zero.")
            .Validate(o => o.ProcessingErrorRetryDelay > TimeSpan.Zero, "Kafka ProcessingErrorRetryDelay deve ser maior que zero.")
            .ValidateOnStart();

        services.AddSingleton<IKafkaDeadLetterProducer, KafkaDeadLetterProducer>();
        services.AddSingleton<LedgerKafkaMessageProcessor>();

        return services;
    }

    public static IServiceCollection AddBalanceLedgerEventsWorker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var kafkaEnabled = configuration.GetValue<bool>("Kafka:Enabled", defaultValue: true);
        if (!kafkaEnabled)
        {
            return services;
        }

        services.AddHostedService<LedgerEventsConsumer>();

        return services;
    }

    private static bool IsLocalEnvironment(IHostEnvironment environment)
        => environment.IsDevelopment()
            || environment.IsEnvironment("Local")
            || environment.IsEnvironment("Test");
}
