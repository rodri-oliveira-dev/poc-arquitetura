using LedgerService.Application.Lancamentos.Commands;
using LedgerService.Domain.Repositories;

using MediatR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LedgerService.Worker.Estornos;

public sealed class EstornoLancamentoProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<EstornoProcessingOptions> _options;
    private readonly ILogger<EstornoLancamentoProcessorService> _logger;

    public EstornoLancamentoProcessorService(
        IServiceProvider serviceProvider,
        IOptions<EstornoProcessingOptions> options,
        ILogger<EstornoLancamentoProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.Value.PollingIntervalSeconds));
        _logger.LogInformation("Worker de estornos iniciado. pollingIntervalSeconds={PollingIntervalSeconds}", interval.TotalSeconds);

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
                _logger.LogError(ex, "Falha no ciclo do worker de estornos.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Worker de estornos parado.");
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
                _logger.LogError(
                    ex,
                    "Falha ao delegar processamento de estorno. estornoId={EstornoId} lancamentoOriginalId={LancamentoOriginalId}",
                    estorno.Id,
                    estorno.LancamentoOriginalId);
            }
        }
    }
}
