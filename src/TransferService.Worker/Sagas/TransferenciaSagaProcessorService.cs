using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using TransferService.Application.Abstractions.Messaging;
using TransferService.Application.Abstractions.Persistence;
using TransferService.Application.Abstractions.Time;
using TransferService.Application.Transferencias.Events;
using TransferService.Domain.Sagas;
using TransferService.Worker.Ledger;
using TransferService.Worker.Options;

namespace TransferService.Worker.Sagas;

public sealed class TransferenciaSagaProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<TransferWorkerOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<TransferenciaSagaProcessorService> _logger;
    private readonly string _lockOwner;

    public TransferenciaSagaProcessorService(
        IServiceProvider serviceProvider,
        IOptions<TransferWorkerOptions> options,
        IClock clock,
        ILogger<TransferenciaSagaProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _clock = clock;
        _logger = logger;
        _lockOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                _logger.LogError(ex, "Erro nao tratado no processamento de Sagas de transferencia.");
            }

            await Task.Delay(_options.Value.PollingInterval, stoppingToken);
        }
    }

    public async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        if (!_options.Value.Enabled)
            return;

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITransferenciaSagaRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = _clock.UtcNow;
        var sagas = await repository.ClaimPendingAsync(
            _options.Value.BatchSize,
            now,
            _lockOwner,
            _options.Value.LockDuration,
            cancellationToken);

        if (sagas.Count == 0)
            return;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var saga in sagas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessSagaSafelyAsync(saga.Id, cancellationToken);
        }
    }

    private async Task ProcessSagaSafelyAsync(Guid sagaId, CancellationToken cancellationToken)
    {
        try
        {
            await ProcessSagaAsync(sagaId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro do Ledger ou persistencia tratado sem derrubar o worker. TransferenciaId={TransferenciaId}", sagaId);
        }
    }

    private async Task ProcessSagaAsync(Guid sagaId, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.TransferServiceDbContext>();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerServiceClient>();
        var outbox = scope.ServiceProvider.GetRequiredService<ITransferenciaOutboxWriter>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var saga = await db.TransferenciasSagas.FirstOrDefaultAsync(x => x.Id == sagaId, cancellationToken);
        if (saga is null || IsFinal(saga.Status))
            return;

        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = saga.CorrelationId,
            ["TransferenciaId"] = saga.Id
        });

        _logger.LogInformation(
            "Processando Saga de transferencia. TransferenciaId={TransferenciaId} CorrelationId={CorrelationId} Status={Status}",
            saga.Id,
            saga.CorrelationId,
            saga.Status);

        if (!saga.DebitCreated)
        {
            await CreateDebitAsync(saga, ledger, outbox, unitOfWork, cancellationToken);
            if (!saga.DebitCreated)
                return;
        }

        if (!saga.CreditCreated)
        {
            await CreateCreditOrCompensateAsync(saga, ledger, outbox, unitOfWork, cancellationToken);
        }
    }

    private async Task CreateDebitAsync(
        TransferenciaSaga saga,
        ILedgerServiceClient ledger,
        ITransferenciaOutboxWriter outbox,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        if (saga.Status == TransferenciaSagaStatus.Processing)
        {
            saga.MarkDebitCreating(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        try
        {
            var result = await ledger.CreateLancamentoAsync(
                new CreateLedgerLancamentoRequest(
                    saga.SourceMerchantId.Value,
                    "DEBIT",
                    -Math.Abs(saga.Amount.Value),
                    saga.Description,
                    saga.ExternalReference),
                IdempotencyKey(saga.Id, "debit"),
                saga.CorrelationId,
                cancellationToken);

            now = _clock.UtcNow;
            saga.MarkDebitCreated(now, result.LancamentoId);
            await outbox.WriteAsync(TransferenciaSagaEventFactory.TransferenciaDebitoCriado(saga, saga.CorrelationId, now), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (LedgerServiceException ex)
        {
            await RegisterRecoverableOrFinalFailureAsync(saga, outbox, unitOfWork, ex.IsTransient, ex.ToString(), cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            await RegisterRecoverableOrFinalFailureAsync(saga, outbox, unitOfWork, true, ex.ToString(), cancellationToken);
        }
        catch (TimeoutException ex)
        {
            await RegisterRecoverableOrFinalFailureAsync(saga, outbox, unitOfWork, true, ex.ToString(), cancellationToken);
        }
    }

    private async Task CreateCreditOrCompensateAsync(
        TransferenciaSaga saga,
        ILedgerServiceClient ledger,
        ITransferenciaOutboxWriter outbox,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        if (saga.Status == TransferenciaSagaStatus.DebitCreated)
        {
            saga.MarkCreditCreating(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        try
        {
            var result = await ledger.CreateLancamentoAsync(
                new CreateLedgerLancamentoRequest(
                    saga.DestinationMerchantId.Value,
                    "CREDIT",
                    Math.Abs(saga.Amount.Value),
                    saga.Description,
                    saga.ExternalReference),
                IdempotencyKey(saga.Id, "credit"),
                saga.CorrelationId,
                cancellationToken);

            now = _clock.UtcNow;
            saga.MarkCompleted(now, result.LancamentoId);
            await outbox.WriteAsync(TransferenciaSagaEventFactory.TransferenciaCreditoCriado(saga, saga.CorrelationId, now), cancellationToken);
            await outbox.WriteAsync(TransferenciaSagaEventFactory.TransferenciaConcluida(saga, saga.CorrelationId, now), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (LedgerServiceException ex)
        {
            await RequestCompensationAsync(saga, ledger, outbox, unitOfWork, ex.ToString(), cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            await RequestCompensationAsync(saga, ledger, outbox, unitOfWork, ex.ToString(), cancellationToken);
        }
        catch (TimeoutException ex)
        {
            await RequestCompensationAsync(saga, ledger, outbox, unitOfWork, ex.ToString(), cancellationToken);
        }
    }

    private async Task RequestCompensationAsync(
        TransferenciaSaga saga,
        ILedgerServiceClient ledger,
        ITransferenciaOutboxWriter outbox,
        IUnitOfWork unitOfWork,
        string reason,
        CancellationToken cancellationToken)
    {
        if (saga.DebitLancamentoId is null)
        {
            await RegisterRecoverableOrFinalFailureAsync(saga, outbox, unitOfWork, true, reason, cancellationToken);
            return;
        }

        try
        {
            var result = await ledger.SolicitarEstornoAsync(
                saga.DebitLancamentoId.Value,
                new SolicitarLedgerEstornoRequest("Compensacao automatica de transferencia apos falha no credito."),
                IdempotencyKey(saga.Id, "compensate-debit"),
                saga.CorrelationId,
                cancellationToken);

            var now = _clock.UtcNow;
            saga.MarkCompensationRequested(now, result.EstornoId);
            await outbox.WriteAsync(TransferenciaSagaEventFactory.TransferenciaCompensacaoSolicitada(saga, saga.CorrelationId, now), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is LedgerServiceException or HttpRequestException or TimeoutException)
        {
            await RegisterRecoverableOrFinalFailureAsync(saga, outbox, unitOfWork, true, ex.ToString(), cancellationToken);
        }
    }

    private async Task RegisterRecoverableOrFinalFailureAsync(
        TransferenciaSaga saga,
        ITransferenciaOutboxWriter outbox,
        IUnitOfWork unitOfWork,
        bool canRetry,
        string reason,
        CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var now = _clock.UtcNow;

        if (canRetry && saga.RetryCount + 1 < options.MaxRetryCount)
        {
            saga.ScheduleRetry(now.Add(options.RetryBackoff), reason, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        saga.MarkFailed(now, reason);
        await outbox.WriteAsync(TransferenciaSagaEventFactory.TransferenciaFalhou(saga, saga.CorrelationId, now), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static bool IsFinal(TransferenciaSagaStatus status)
        => status is TransferenciaSagaStatus.Completed
            or TransferenciaSagaStatus.Compensated
            or TransferenciaSagaStatus.Failed
            or TransferenciaSagaStatus.Rejected;

    private static string IdempotencyKey(Guid sagaId, string step)
        => $"transferencia:{sagaId}:{step}";
}
