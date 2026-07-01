namespace TransferService.Worker.HostedServices;

public sealed partial class WorkerLifecycleLogService(
    string serviceName,
    ILogger<WorkerLifecycleLogService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        LogWorkerStarted(logger, serviceName);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        LogWorkerStopped(logger, serviceName);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Processo worker iniciado. serviceName={ServiceName}")]
    private static partial void LogWorkerStarted(ILogger logger, string serviceName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Processo worker parado. serviceName={ServiceName}")]
    private static partial void LogWorkerStopped(ILogger logger, string serviceName);
}
