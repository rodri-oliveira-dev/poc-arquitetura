using LedgerService.Domain.Repositories;
using LedgerService.Infrastructure.Estornos;
using LedgerService.Infrastructure.Messaging.Kafka;
using LedgerService.Infrastructure.Observability;
using LedgerService.Infrastructure.Outbox;
using LedgerService.Infrastructure.Persistence;
using LedgerService.Infrastructure.Repositories;
using LedgerService.Infrastructure.Reprocessamentos;
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
        services
            .AddLedgerApiInfrastructure(configuration, environment)
            .AddLedgerEstornoWorker(configuration)
            .AddLedgerOutboxWorker(configuration)
            .AddLedgerReprocessamentoWorker(configuration, environment);

        return services;
    }

    public static IServiceCollection AddLedgerApiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddLedgerInfrastructureCommon()
            .AddLedgerPersistence(configuration)
            .AddLedgerRepositories()
            .AddLedgerKafkaProducer(configuration, environment);

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

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

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

    public static IServiceCollection AddLedgerKafkaProducer(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var kafkaEnabled = configuration.GetValue<bool>("Kafka:Enabled", defaultValue: true);
        if (!kafkaEnabled)
        {
            return services;
        }

        services.AddOptions<KafkaProducerOptions>()
            .Bind(configuration.GetSection(KafkaProducerOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers), "Kafka BootstrapServers nao configurado.")
            .Validate(o => IsLocalEnvironment(environment) || !KafkaClientConfigExtensions.IsPlaintext(o), "Kafka PLAINTEXT e permitido apenas em Development/Local.")
            .ValidateOnStart();

        services.AddSingleton<IOutboxEventProducer, OutboxKafkaProducer>();

        return services;
    }

    public static IServiceCollection AddLedgerOutboxWorker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Outbox/Kafka e opcional no ambiente de testes de integracao/locais.
        // Caso contrario, o ValidateOnStart + HostedService impedem a API de subir sem Kafka.
        var kafkaEnabled = configuration.GetValue<bool>("Kafka:Enabled", defaultValue: true);
        if (!kafkaEnabled)
        {
            return services;
        }

        services.AddOptions<OutboxPublisherOptions>()
            .Bind(configuration.GetSection(OutboxPublisherOptions.SectionName))
            .ValidateOnStart();

        services.AddHostedService<OutboxKafkaPublisherService>();

        return services;
    }

    public static IServiceCollection AddLedgerEstornoWorker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var estornoProcessorEnabled = configuration.GetValue<bool>("Estornos:Processor:Enabled", defaultValue: true);
        if (!estornoProcessorEnabled)
        {
            return services;
        }

        services.AddOptions<EstornoProcessingOptions>()
            .Bind(configuration.GetSection(EstornoProcessingOptions.SectionName))
            .Validate(o => o.PollingIntervalSeconds > 0, "Estornos Processor PollingIntervalSeconds deve ser maior que zero.")
            .Validate(o => o.BatchSize > 0, "Estornos Processor BatchSize deve ser maior que zero.")
            .ValidateOnStart();

        services.AddHostedService<EstornoLancamentoProcessorService>();

        return services;
    }

    public static IServiceCollection AddLedgerReprocessamentoWorker(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var kafkaEnabled = configuration.GetValue<bool>("Kafka:Enabled", defaultValue: true);
        if (!kafkaEnabled)
        {
            return services;
        }

        var reprocessamentoConsumerEnabled = configuration.GetValue<bool>(
            $"{ReprocessamentoLancamentosConsumerOptions.SectionName}:Enabled",
            defaultValue: true);
        if (!reprocessamentoConsumerEnabled)
        {
            return services;
        }

        services.AddOptions<ReprocessamentoLancamentosConsumerOptions>()
            .Bind(configuration.GetSection(ReprocessamentoLancamentosConsumerOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers), "Reprocessamentos Consumer BootstrapServers nao configurado.")
            .Validate(o => IsLocalEnvironment(environment) || !KafkaClientConfigExtensions.IsPlaintext(o), "Kafka PLAINTEXT e permitido apenas em Development/Local.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.GroupId), "Reprocessamentos Consumer GroupId nao configurado.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Topic), "Reprocessamentos Consumer Topic nao configurado.")
            .Validate(o => o.ConsumeErrorRetryDelay > TimeSpan.Zero, "Reprocessamentos Consumer ConsumeErrorRetryDelay deve ser maior que zero.")
            .Validate(o => o.ProcessingErrorRetryDelay > TimeSpan.Zero, "Reprocessamentos Consumer ProcessingErrorRetryDelay deve ser maior que zero.")
            .ValidateOnStart();

        services.AddSingleton<ReprocessamentoLancamentosMessageProcessor>();
        services.AddHostedService<ReprocessamentoLancamentosConsumerService>();

        return services;
    }

    private static bool IsLocalEnvironment(IHostEnvironment environment)
        => environment.IsDevelopment()
            || environment.IsEnvironment("Local")
            || environment.IsEnvironment("Test");
}
