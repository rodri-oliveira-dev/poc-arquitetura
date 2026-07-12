using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using MediatR;

using Microsoft.Extensions.Options;

using PaymentService.Application.Abstractions.Persistence;
using PaymentService.Application.Abstractions.Time;
using PaymentService.Application.Payments.InboxProcessing;
using PaymentService.Application.Payments.Webhooks;
using PaymentService.Worker.Observability;
using PaymentService.Worker.Options;

namespace PaymentService.Worker.HostedServices;

[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Background workers must isolate poll and message failures.")]
[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Structured logs are intentionally simple in this POC worker.")]
[SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "Logged values are cheap scalar values in this worker.")]
public sealed class PaymentInboxWorkerService(
    IServiceProvider serviceProvider,
    IOptions<PaymentInboxWorkerOptions> options,
    IClock clock,
    PaymentInboxWorkerMetrics metrics,
    ILogger<PaymentInboxWorkerService> logger) : BackgroundService
{
    public const string ActivitySourceName = "PaymentService.InboxWorker";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IOptions<PaymentInboxWorkerOptions> _options = options;
    private readonly IClock _clock = clock;
    private readonly PaymentInboxWorkerMetrics _metrics = metrics;
    private readonly ILogger<PaymentInboxWorkerService> _logger = logger;
    private readonly string _lockOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Payment Inbox Worker iniciado com LockOwner {LockOwner} e polling {PollingInterval}.",
            _lockOwner,
            _options.Value.PollingInterval);

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
                _logger.LogError(exception, "Erro inesperado no polling da Inbox de pagamentos.");
            }

            try
            {
                await Task.Delay(_options.Value.PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Payment Inbox Worker finalizado para LockOwner {LockOwner}.", _lockOwner);
    }

    internal async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("payment.inbox.poll", ActivityKind.Consumer);
        using var scope = _serviceProvider.CreateScope();

        var inboxRepository = scope.ServiceProvider.GetRequiredService<IPaymentInboxRepository>();
        var workerOptions = _options.Value;
        var now = _clock.UtcNow;

        var backlog = await inboxRepository.CountBacklogAsync(now, cancellationToken);
        _metrics.SetBacklog(backlog);

        _logger.LogInformation(
            "Polling da Inbox de pagamentos iniciado. BatchSize {BatchSize}, Backlog {Backlog}.",
            workerOptions.BatchSize,
            backlog);

        var claimed = await inboxRepository.ClaimEligibleAsync(
            workerOptions.BatchSize,
            now,
            _lockOwner,
            workerOptions.ProcessingLeaseTimeout,
            cancellationToken);

        var recoveredLeaseCount = claimed.Count(x => x.AttemptCount > 1 && x.Status == PaymentInboxStatus.Processing);
        _metrics.RecordClaim(claimed.Count, recoveredLeaseCount);
        activity?.SetTag("payment.inbox.claimed_count", claimed.Count);
        activity?.SetTag("payment.inbox.recovered_lease_count", recoveredLeaseCount);

        if (claimed.Count == 0)
            return;

        _logger.LogInformation(
            "{Count} mensagens da Inbox de pagamentos reclamadas por {LockOwner}.",
            claimed.Count,
            _lockOwner);

        foreach (var message in claimed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessMessageAsync(message.Id, cancellationToken);
        }
    }

    private async Task ProcessMessageAsync(Guid inboxMessageId, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var startedAt = Stopwatch.GetTimestamp();

        using var activity = ActivitySource.StartActivity("payment.inbox.process", ActivityKind.Consumer);
        activity?.SetTag("payment.inbox.message_id", inboxMessageId.ToString());

        _logger.LogInformation("Processamento da Inbox de pagamentos iniciado para {InboxMessageId}.", inboxMessageId);

        try
        {
            var result = await mediator.Send(
                new ProcessPaymentInboxMessageCommand(inboxMessageId, _lockOwner),
                cancellationToken);

            var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            _metrics.RecordProcess(result.Outcome, elapsed);
            activity?.SetTag("payment.inbox.outcome", result.Outcome);
            activity?.SetTag("payment.inbox.status", result.Status.ToString());

            _logger.LogInformation(
                "Processamento da Inbox de pagamentos concluido para {InboxMessageId}. Status {Status}, Outcome {Outcome}, PaymentChanged {PaymentChanged}.",
                result.InboxMessageId,
                result.Status,
                result.Outcome,
                result.PaymentChanged);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _metrics.RecordFailure("unexpected_message_error");
            _logger.LogError(exception, "Erro inesperado ao processar InboxMessage {InboxMessageId}.", inboxMessageId);
            await ScheduleUnexpectedFailureAsync(inboxMessageId, exception, cancellationToken);
        }
    }

    private async Task ScheduleUnexpectedFailureAsync(
        Guid inboxMessageId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var inboxRepository = scope.ServiceProvider.GetRequiredService<IPaymentInboxRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var workerOptions = _options.Value;
        var now = _clock.UtcNow;
        var nextRetryAt = PaymentInboxRetryPolicy.CalculateNextRetryAt(
            now,
            1,
            workerOptions.BaseRetryDelay,
            workerOptions.MaxRetryDelay);

        var status = await inboxRepository.MarkFailedProcessingAttemptAsync(
            inboxMessageId,
            workerOptions.MaxRetryCount,
            now,
            nextRetryAt,
            BuildSafeUnexpectedError(exception),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Falha inesperada da InboxMessage {InboxMessageId} marcada como {Status}.",
            inboxMessageId,
            status);
    }

    private static string BuildSafeUnexpectedError(Exception exception)
        => exception switch
        {
            TimeoutException => "Unexpected timeout while processing payment inbox message.",
            _ => "Unexpected technical failure while processing payment inbox message."
        };
}
