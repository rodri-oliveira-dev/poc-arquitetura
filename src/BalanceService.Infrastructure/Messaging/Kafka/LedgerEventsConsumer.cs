using BalanceService.Application.Balances.Commands;
using BalanceService.Domain.Balances;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace BalanceService.Infrastructure.Messaging.Kafka;

public sealed class LedgerEventsConsumer : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("BalanceService.KafkaConsumer");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceProvider _serviceProvider;
    private readonly KafkaConsumerOptions _options;
    private readonly ILogger<LedgerEventsConsumer> _logger;

    public LedgerEventsConsumer(
        IServiceProvider serviceProvider,
        IOptions<KafkaConsumerOptions> options,
        ILogger<LedgerEventsConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateOptions(_options);

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            ClientId = _options.ClientId,
            EnableAutoCommit = _options.EnableAutoCommit,
            EnableAutoOffsetStore = _options.EnableAutoOffsetStore,
            AllowAutoCreateTopics = _options.AllowAutoCreateTopics,
            AutoOffsetReset = ParseAutoOffsetReset(_options.AutoOffsetReset)
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) =>
            {
                _logger.LogWarning("Kafka consumer error: {Reason} (IsFatal={IsFatal})", e.Reason, e.IsFatal);
            })
            .Build();

        consumer.Subscribe(_options.Topics);

        _logger.LogInformation(
            "LedgerEventsConsumer started (groupId={GroupId}, clientId={ClientId}, topics={Topics})",
            _options.GroupId,
            _options.ClientId,
            string.Join(",", _options.Topics));

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null)
                    continue;

                var evt = JsonSerializer.Deserialize<LedgerEntryCreatedEvent>(result.Message.Value, JsonOptions);
                if (evt is null)
                {
                    _logger.LogWarning(
                        "Mensagem inválida (não foi possível desserializar). topic={Topic} partition={Partition} offset={Offset}",
                        result.Topic,
                        result.Partition.Value,
                        result.Offset.Value);
                    // não commitamos para retry; mas pode causar loop. Backoff reduz tight loop.
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                using var logScope = _logger.BeginScope(new Dictionary<string, object?>
                {
                    ["CorrelationId"] = evt.CorrelationId,
                    ["EventId"] = evt.Id,
                    ["MerchantId"] = evt.MerchantId,
                    ["OccurredAt"] = evt.OccurredAt,
                    ["KafkaTopic"] = result.Topic,
                    ["KafkaPartition"] = result.Partition.Value,
                    ["KafkaOffset"] = result.Offset.Value
                });

                using var activity = ActivitySource.StartActivity("kafka.consume", ActivityKind.Consumer);
                activity?.SetTag("messaging.system", "kafka");
                activity?.SetTag("messaging.operation", "consume");
                activity?.SetTag("messaging.destination", result.Topic);
                activity?.SetTag("messaging.kafka.partition", result.Partition.Value);
                activity?.SetTag("messaging.kafka.offset", result.Offset.Value);
                activity?.SetTag("correlation_id", evt.CorrelationId);
                activity?.AddBaggage("correlation_id", evt.CorrelationId);
                activity?.SetTag("event_id", evt.Id);

                await ProcessMessageAsync(evt, stoppingToken);

                consumer.Commit(result);
                _logger.LogDebug("Mensagem processada e offset commitado");
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Erro ao consumir do Kafka. Vai retentar.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Erro ao processar mensagem do Kafka. Offset não será commitado (retry). topic={Topic} partition={Partition} offset={Offset}",
                    result?.Topic,
                    result?.Partition.Value,
                    result?.Offset.Value);

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        try
        {
            consumer.Close();
        }
        catch
        {
            // ignore shutdown errors
        }

        _logger.LogInformation("LedgerEventsConsumer stopped");
    }

    private async Task ProcessMessageAsync(LedgerEntryCreatedEvent evt, CancellationToken ct)
    {
        // escopo por mensagem para evitar concorrência no DbContext
        using var scope = _serviceProvider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new ApplyLedgerEntryCreatedCommand(evt), ct);
    }

    private static void ValidateOptions(KafkaConsumerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BootstrapServers))
            throw new InvalidOperationException("Kafka BootstrapServers não configurado.");

        if (string.IsNullOrWhiteSpace(options.GroupId))
            throw new InvalidOperationException("Kafka GroupId não configurado.");

        if (options.Topics is null || options.Topics.Count == 0)
            throw new InvalidOperationException("Kafka Topics não configurado.");
    }

    private static AutoOffsetReset ParseAutoOffsetReset(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "earliest" => AutoOffsetReset.Earliest,
            "latest" => AutoOffsetReset.Latest,
            _ => AutoOffsetReset.Earliest
        };
    }
}
