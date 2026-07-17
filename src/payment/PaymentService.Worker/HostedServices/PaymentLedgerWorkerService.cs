using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Options;

using PaymentService.Application.Payments.Ledger;
using PaymentService.Worker.Observability;
using PaymentService.Worker.Options;

namespace PaymentService.Worker.HostedServices;

[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Background workers must isolate polling failures.")]
public sealed partial class PaymentLedgerWorkerService(
    IServiceProvider serviceProvider,
    IOptions<PaymentLedgerWorkerOptions> options,
    TimeProvider timeProvider,
    PaymentLedgerWorkerMetrics metrics,
    ILogger<PaymentLedgerWorkerService> logger) : BackgroundService
{
    public const string ActivitySourceName = "PaymentService.LedgerWorker";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IOptions<PaymentLedgerWorkerOptions> _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly PaymentLedgerWorkerMetrics _metrics = metrics;
    private readonly ILogger<PaymentLedgerWorkerService> _logger = logger;
    private readonly string _lockOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(_logger, _lockOwner, _options.Value.PollingInterval);

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
            catch (Exception exception)
            {
                _metrics.RecordFailure("unexpected_poll_error");
                LogUnexpectedPollError(_logger, exception);
            }

            try
            {
                await Task.Delay(_options.Value.PollingInterval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        LogStopped(_logger, _lockOwner);
    }

    internal async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("payment.ledger.process", ActivityKind.Internal);
        using var scope = _serviceProvider.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IPaymentLedgerProcessor>();
        var workerOptions = _options.Value;
        var startedAt = Stopwatch.GetTimestamp();

        var result = await processor.ProcessBatchAsync(
            workerOptions.BatchSize,
            _lockOwner,
            workerOptions.ProcessingLeaseTimeout,
            cancellationToken);

        var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        _metrics.RecordBatch(
            new PaymentLedgerWorkerBatchMetrics(
                result.Claimed,
                result.Completed,
                result.RetryScheduled,
                result.FailedDefinitive,
                result.DeadLettered),
            elapsed);

        activity?.SetTag("payment.ledger.claimed", result.Claimed);
        activity?.SetTag("payment.ledger.completed", result.Completed);
        activity?.SetTag("payment.ledger.retry_scheduled", result.RetryScheduled);
        activity?.SetTag("payment.ledger.failed_definitive", result.FailedDefinitive);
        activity?.SetTag("payment.ledger.deadlettered", result.DeadLettered);

        if (result.Claimed > 0)
            LogBatchProcessed(_logger, result.Claimed, result.Completed, result.RetryScheduled, result.FailedDefinitive, result.DeadLettered);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Payment Ledger Worker iniciado com LockOwner {LockOwner} e polling {PollingInterval}.")]
    private static partial void LogStarted(ILogger logger, string lockOwner, TimeSpan pollingInterval);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Payment Ledger Worker finalizado para LockOwner {LockOwner}.")]
    private static partial void LogStopped(ILogger logger, string lockOwner);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Erro inesperado no polling da integracao Payment -> Ledger.")]
    private static partial void LogUnexpectedPollError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Integracao Payment -> Ledger processada. claimed={Claimed} completed={Completed} retryScheduled={RetryScheduled} failedDefinitive={FailedDefinitive} deadLettered={DeadLettered}")]
    private static partial void LogBatchProcessed(
        ILogger logger,
        int claimed,
        int completed,
        int retryScheduled,
        int failedDefinitive,
        int deadLettered);
}
