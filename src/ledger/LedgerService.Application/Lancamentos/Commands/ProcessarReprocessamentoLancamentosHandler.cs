using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

using LedgerService.Application.Abstractions.Time;
using LedgerService.Application.Common.Exceptions;
using LedgerService.Application.Common.Observability;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Application.Lancamentos.Services;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Exceptions;
using LedgerService.Domain.Repositories;

using MediatR;

using Microsoft.Extensions.Logging;

namespace LedgerService.Application.Lancamentos.Commands;

public sealed partial class ProcessarReprocessamentoLancamentosHandler
    : IRequestHandler<ProcessarReprocessamentoLancamentosCommand>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Falha tecnica ao processar reprocessamento {ReprocessamentoId}")]
    private static partial void LogTechnicalFailure(ILogger logger, Exception exception, Guid reprocessamentoId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Reprocessamento processado. reprocessamentoId={ReprocessamentoId} merchantId={MerchantId} dataInicial={DataInicial} dataFinal={DataFinal} statusAnterior={StatusAnterior} statusNovo={StatusNovo} registrosEncontrados={RegistrosEncontrados} registrosReprocessados={RegistrosReprocessados} duracaoMs={DuracaoMs}")]
    private static partial void LogReprocessamentoProcessed(
        ILogger logger,
        Guid reprocessamentoId,
        string merchantId,
        string dataInicial,
        string dataFinal,
        ReprocessamentoLancamentosStatus statusAnterior,
        ReprocessamentoLancamentosStatus statusNovo,
        int registrosEncontrados,
        int registrosReprocessados,
        long duracaoMs);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Reprocessamento rejeitado. reprocessamentoId={ReprocessamentoId} motivo={Motivo}")]
    private static partial void LogReprocessamentoRejected(ILogger logger, Guid reprocessamentoId, string motivo);

    private readonly IReprocessamentoLancamentosRepository _reprocessamentoRepository;
    private readonly ILedgerEntryRepository _ledgerEntryRepository;
    private readonly IOutboxMessageRepository _outboxMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessarReprocessamentoLancamentosHandler> _logger;
    private readonly IClock _clock;
    private readonly LedgerDomainMetrics? _metrics;

    public ProcessarReprocessamentoLancamentosHandler(
        IReprocessamentoLancamentosRepository reprocessamentoRepository,
        ILedgerEntryRepository ledgerEntryRepository,
        IOutboxMessageRepository outboxMessageRepository,
        IUnitOfWork unitOfWork,
        ILogger<ProcessarReprocessamentoLancamentosHandler> logger,
        IClock? clock = null,
        LedgerDomainMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(reprocessamentoRepository);
        ArgumentNullException.ThrowIfNull(ledgerEntryRepository);
        ArgumentNullException.ThrowIfNull(outboxMessageRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);

        _reprocessamentoRepository = reprocessamentoRepository;
        _ledgerEntryRepository = ledgerEntryRepository;
        _outboxMessageRepository = outboxMessageRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _clock = clock ?? new SystemClock();
        _metrics = metrics;
    }

    public async Task Handle(ProcessarReprocessamentoLancamentosCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            await ProcessInternalAsync(request.ReprocessamentoId, cancellationToken);
        }
        catch (DomainException ex)
        {
            await MarkRejectedAsync(request.ReprocessamentoId, ex.Message, cancellationToken);
        }
        catch (NotFoundException ex)
        {
            await MarkRejectedAsync(request.ReprocessamentoId, ex.Message, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await MarkFailedAsync(
                request.ReprocessamentoId,
                "Falha tecnica ao processar reprocessamento.",
                cancellationToken);
            LogTechnicalFailure(_logger, ex, request.ReprocessamentoId);
        }
    }

    private async Task ProcessInternalAsync(Guid reprocessamentoId, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var reprocessamento = await _reprocessamentoRepository.GetByIdForUpdateAsync(
            reprocessamentoId,
            cancellationToken);
        if (reprocessamento is null)
            throw new NotFoundException("Solicitacao de reprocessamento nao encontrada.");

        if (reprocessamento.IsFinal())
        {
            await transaction.CommitAsync(cancellationToken);
            _metrics?.RecordReprocessRequestProcessed(ToMetricResult(reprocessamento.Status));
            return;
        }

        var previousStatus = reprocessamento.Status;
        var now = _clock.UtcNow.UtcDateTime;
        reprocessamento.MarkProcessing(now);

        var startInclusive = reprocessamento.DataInicial.ToDateTime(TimeOnly.MinValue);
        var endExclusive = reprocessamento.DataFinal.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var entries = await _ledgerEntryRepository.ListByMerchantAndPeriodAsync(
            reprocessamento.MerchantId,
            startInclusive,
            endExclusive,
            cancellationToken);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outboxPayload = JsonSerializer.Serialize(ToLedgerEntryCreated(entry), JsonOptions);
            var traceContext = OutboxTraceContext.CaptureCurrent();
            var outboxMessage = new OutboxMessage(
                "LedgerEntryReprocessamento",
                entry.Id,
                LedgerEntryCreatedV2.EventType,
                outboxPayload,
                now,
                reprocessamento.CorrelationId,
                traceContext.TraceParent,
                traceContext.TraceState,
                traceContext.Baggage);

            await _outboxMessageRepository.AddAsync(outboxMessage, cancellationToken);
        }

        if (entries.Count == 0)
        {
            reprocessamento.CompleteWithWarnings(
                "Nenhum lancamento encontrado para o criterio informado.",
                now);
        }
        else
        {
            reprocessamento.Complete(now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _metrics?.RecordReprocessRequestProcessed(ToMetricResult(reprocessamento.Status));

        stopwatch.Stop();
        if (_logger.IsEnabled(LogLevel.Information))
        {
            var dataInicial = reprocessamento.DataInicial.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var dataFinal = reprocessamento.DataFinal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            LogReprocessamentoProcessed(
                _logger,
                reprocessamento.Id,
                reprocessamento.MerchantId,
                dataInicial,
                dataFinal,
                previousStatus,
                reprocessamento.Status,
                entries.Count,
                entries.Count,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task MarkRejectedAsync(Guid reprocessamentoId, string reason, CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var reprocessamento = await _reprocessamentoRepository.GetByIdForUpdateAsync(
            reprocessamentoId,
            cancellationToken);
        if (reprocessamento is not null && !reprocessamento.IsFinal())
        {
            reprocessamento.Reject(reason, _clock.UtcNow.UtcDateTime);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        _metrics?.RecordReprocessRequestProcessed("rejected");
        LogReprocessamentoRejected(_logger, reprocessamentoId, reason);
    }

    private async Task MarkFailedAsync(Guid reprocessamentoId, string reason, CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var reprocessamento = await _reprocessamentoRepository.GetByIdForUpdateAsync(
            reprocessamentoId,
            cancellationToken);
        if (reprocessamento is not null && !reprocessamento.IsFinal())
        {
            reprocessamento.Fail(reason, _clock.UtcNow.UtcDateTime);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        _metrics?.RecordReprocessRequestProcessed("failed");
    }

    private static LedgerEntryCreatedV2 ToLedgerEntryCreated(LedgerEntry entry)
        => new(
            $"lan_{entry.Id.ToString("N")[..8]}",
            entry.Type == LedgerEntryType.Credit ? "CREDIT" : "DEBIT",
            entry.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            LedgerEntryCreatedEventFactory.SupportedCurrency,
            entry.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
            entry.MerchantId,
            entry.OccurredAt.ToString("o", CultureInfo.InvariantCulture),
            entry.Description,
            entry.CorrelationId.ToString(),
            entry.ExternalReference);

    private static string ToMetricResult(ReprocessamentoLancamentosStatus status)
        => status switch
        {
            ReprocessamentoLancamentosStatus.Completed => "completed",
            ReprocessamentoLancamentosStatus.CompletedWithWarnings => "completed_with_warnings",
            ReprocessamentoLancamentosStatus.Rejected => "rejected",
            ReprocessamentoLancamentosStatus.Failed => "failed",
            _ => "failed"
        };
}
