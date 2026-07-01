using AuditService.Worker.Options;

using Microsoft.Extensions.Options;

namespace AuditService.Worker.HostedServices;

internal sealed partial class AuditWorkerPlaceholderService(
    IOptions<AuditWorkerOptions> options,
    ILogger<AuditWorkerPlaceholderService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.Value.Enabled)
        {
            LogWorkerEnabled(logger);
            return Task.CompletedTask;
        }

        LogWorkerDisabled(logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        LogWorkerStopped(logger);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "AuditService.Worker iniciado sem consumer configurado.")]
    private static partial void LogWorkerEnabled(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "AuditService.Worker registrado em modo placeholder; nenhum evento sera consumido.")]
    private static partial void LogWorkerDisabled(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "AuditService.Worker parado.")]
    private static partial void LogWorkerStopped(ILogger logger);
}
