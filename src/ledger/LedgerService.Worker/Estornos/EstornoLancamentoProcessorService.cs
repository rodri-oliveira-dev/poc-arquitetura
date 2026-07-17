using LedgerService.Application.Lancamentos.Commands;
using LedgerService.Domain.Repositories;

using MediatR;

using Microsoft.Extensions.Options;

namespace LedgerService.Worker.Estornos;

public sealed partial class EstornoLancamentoProcessorService(
    IServiceProvider serviceProvider,
    IOptions<EstornoProcessingOptions> options,
    TimeProvider timeProvider,
    ILogger<EstornoLancamentoProcessorService> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IOptions<EstornoProcessingOptions> _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<EstornoLancamentoProcessorService> _logger = logger;

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Worker de estornos iniciado. pollingIntervalSeconds={PollingIntervalSeconds}")]
    private static partial void LogWorkerStarted(ILogger logger, double pollingIntervalSeconds);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Falha no ciclo do worker de estornos.")]
    private static partial void LogWorkerCycleFailure(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Worker de estornos parado.")]
    private static partial void LogWorkerStopped(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Falha ao delegar processamento de estorno. estornoId={EstornoId} lancamentoOriginalId={LancamentoOriginalId}")]
    private static partial void LogEstornoProcessingDelegationFailure(
        ILogger logger,
        Exception exception,
        Guid estornoId,
        Guid lancamentoOriginalId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.Value.PollingIntervalSeconds));
        LogWorkerStarted(_logger, interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogWorkerCycleFailure(_logger, ex);
            }

            try
            {
                await Task.Delay(interval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        LogWorkerStopped(_logger);
    }

    public async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var estornoRepository = scope.ServiceProvider.GetRequiredService<IEstornoLancamentoRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var pending = await estornoRepository.ClaimPendingAsync(
            Math.Max(1, _options.Value.BatchSize),
            cancellationToken);

        foreach (var estorno in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await sender.Send(new ProcessarEstornoLancamentoCommand(estorno.Id), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogEstornoProcessingDelegationFailure(
                    _logger,
                    ex,
                    estorno.Id,
                    estorno.LancamentoOriginalId);
            }
        }
    }
}
