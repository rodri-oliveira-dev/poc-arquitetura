namespace BalanceService.Worker;

public sealed class WorkerLifecycleLogService : IHostedService
{
    private readonly string _serviceName;
    private readonly ILogger<WorkerLifecycleLogService> _logger;

    public WorkerLifecycleLogService(string serviceName, ILogger<WorkerLifecycleLogService> logger)
    {
        _serviceName = serviceName;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processo worker iniciado. serviceName={ServiceName}", _serviceName);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processo worker parado. serviceName={ServiceName}", _serviceName);
        return Task.CompletedTask;
    }
}
