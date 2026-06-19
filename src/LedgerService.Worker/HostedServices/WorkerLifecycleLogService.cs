namespace LedgerService.Worker.HostedServices;

public sealed partial class WorkerLifecycleLogService : IHostedService
{
    private readonly string _serviceName;
    private readonly ILogger<WorkerLifecycleLogService> _logger;

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Processo worker iniciado. serviceName={ServiceName}")]
    private static partial void LogWorkerStarted(ILogger logger, string serviceName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Processo worker parado. serviceName={ServiceName}")]
    private static partial void LogWorkerStopped(ILogger logger, string serviceName);

    public WorkerLifecycleLogService(string serviceName, ILogger<WorkerLifecycleLogService> logger)
    {
        _serviceName = serviceName;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        LogWorkerStarted(_logger, _serviceName);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        LogWorkerStopped(_logger, _serviceName);
        return Task.CompletedTask;
    }
}
