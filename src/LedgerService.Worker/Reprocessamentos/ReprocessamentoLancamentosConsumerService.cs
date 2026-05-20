using Confluent.Kafka;

using LedgerService.Worker.Messaging.Kafka;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LedgerService.Worker.Reprocessamentos;

public sealed class ReprocessamentoLancamentosConsumerService : BackgroundService
{
    private readonly ReprocessamentoLancamentosConsumerOptions _options;
    private readonly ReprocessamentoLancamentosMessageProcessor _messageProcessor;
    private readonly ILogger<ReprocessamentoLancamentosConsumerService> _logger;

    public ReprocessamentoLancamentosConsumerService(
        IOptions<ReprocessamentoLancamentosConsumerOptions> options,
        ReprocessamentoLancamentosMessageProcessor messageProcessor,
        ILogger<ReprocessamentoLancamentosConsumerService> logger)
    {
        _options = options.Value;
        _messageProcessor = messageProcessor;
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
        config.ApplySecurity(_options);

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) =>
            {
                _logger.LogWarning(
                    "Kafka consumer de reprocessamento reportou erro: {Reason} (IsFatal={IsFatal})",
                    e.Reason,
                    e.IsFatal);
            })
            .Build();

        consumer.Subscribe(_options.Topic);
        _logger.LogInformation(
            "Consumer de reprocessamento iniciado. groupId={GroupId} clientId={ClientId} topic={Topic}",
            _options.GroupId,
            _options.ClientId,
            _options.Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null)
                    continue;

                if (await _messageProcessor.ProcessAsync(result, stoppingToken))
                    consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Falha ao consumir mensagem de reprocessamento.");
                await Task.Delay(_options.ConsumeErrorRetryDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (KafkaException ex)
            {
                _logger.LogError(
                    ex,
                    "Falha ao processar mensagem de reprocessamento. topic={Topic} partition={Partition} offset={Offset}",
                    result?.Topic,
                    result?.Partition.Value,
                    result?.Offset.Value);
                await Task.Delay(_options.ProcessingErrorRetryDelay, stoppingToken);
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(
                    ex,
                    "Timeout ao processar mensagem de reprocessamento. topic={Topic} partition={Partition} offset={Offset}",
                    result?.Topic,
                    result?.Partition.Value,
                    result?.Offset.Value);
                await Task.Delay(_options.ProcessingErrorRetryDelay, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Falha inesperada ao processar mensagem de reprocessamento. topic={Topic} partition={Partition} offset={Offset}",
                    result?.Topic,
                    result?.Partition.Value,
                    result?.Offset.Value);
                await Task.Delay(_options.ProcessingErrorRetryDelay, stoppingToken);
            }
        }

        try
        {
            consumer.Close();
        }
        catch (KafkaException)
        {
            // ignore shutdown errors
        }

        _logger.LogInformation("Consumer de reprocessamento parado.");
    }

    internal static void ValidateOptions(ReprocessamentoLancamentosConsumerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BootstrapServers))
            throw new InvalidOperationException("Reprocessamentos Consumer BootstrapServers nao configurado.");

        if (string.IsNullOrWhiteSpace(options.GroupId))
            throw new InvalidOperationException("Reprocessamentos Consumer GroupId nao configurado.");

        if (string.IsNullOrWhiteSpace(options.Topic))
            throw new InvalidOperationException("Reprocessamentos Consumer Topic nao configurado.");

        if (options.ConsumeErrorRetryDelay <= TimeSpan.Zero)
            throw new InvalidOperationException("Reprocessamentos Consumer ConsumeErrorRetryDelay deve ser maior que zero.");

        if (options.ProcessingErrorRetryDelay <= TimeSpan.Zero)
            throw new InvalidOperationException("Reprocessamentos Consumer ProcessingErrorRetryDelay deve ser maior que zero.");
    }

    internal static AutoOffsetReset ParseAutoOffsetReset(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "earliest" => AutoOffsetReset.Earliest,
            "latest" => AutoOffsetReset.Latest,
            _ => AutoOffsetReset.Earliest
        };
    }
}
