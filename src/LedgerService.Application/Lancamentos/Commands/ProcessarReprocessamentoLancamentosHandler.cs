using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

using LedgerService.Application.Common.Exceptions;
using LedgerService.Application.Common.Observability;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Exceptions;
using LedgerService.Domain.Repositories;

using MediatR;

using Microsoft.Extensions.Logging;

namespace LedgerService.Application.Lancamentos.Commands;

public sealed class ProcessarReprocessamentoLancamentosHandler
    : IRequestHandler<ProcessarReprocessamentoLancamentosCommand>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IReprocessamentoLancamentosRepository _reprocessamentoRepository;
    private readonly ILedgerEntryRepository _ledgerEntryRepository;
    private readonly IOutboxMessageRepository _outboxMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessarReprocessamentoLancamentosHandler> _logger;
    private readonly LedgerDomainMetrics? _metrics;

    public ProcessarReprocessamentoLancamentosHandler(
        IReprocessamentoLancamentosRepository reprocessamentoRepository,
        ILedgerEntryRepository ledgerEntryRepository,
        IOutboxMessageRepository outboxMessageRepository,
        IUnitOfWork unitOfWork,
        ILogger<ProcessarReprocessamentoLancamentosHandler> logger,
        LedgerDomainMetrics? metrics = null)
    {
        _reprocessamentoRepository = reprocessamentoRepository;
        _ledgerEntryRepository = ledgerEntryRepository;
        _outboxMessageRepository = outboxMessageRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task Handle(ProcessarReprocessamentoLancamentosCommand request, CancellationToken cancellationToken)
    {
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
            _logger.LogError(
                ex,
                "Falha tecnica ao processar reprocessamento {ReprocessamentoId}",
                request.ReprocessamentoId);
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
        reprocessamento.MarkProcessing(DateTime.Now);

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
                LedgerEntryCreatedV1.EventType,
                outboxPayload,
                DateTime.Now,
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
                DateTime.Now);
        }
        else
        {
            reprocessamento.Complete(DateTime.Now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _metrics?.RecordReprocessRequestProcessed(ToMetricResult(reprocessamento.Status));

        stopwatch.Stop();
        _logger.LogInformation(
            "Reprocessamento processado. reprocessamentoId={ReprocessamentoId} merchantId={MerchantId} dataInicial={DataInicial} dataFinal={DataFinal} statusAnterior={StatusAnterior} statusNovo={StatusNovo} registrosEncontrados={RegistrosEncontrados} registrosReprocessados={RegistrosReprocessados} duracaoMs={DuracaoMs}",
            reprocessamento.Id,
            reprocessamento.MerchantId,
            reprocessamento.DataInicial.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            reprocessamento.DataFinal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            previousStatus,
            reprocessamento.Status,
            entries.Count,
            entries.Count,
            stopwatch.ElapsedMilliseconds);
    }

    private async Task MarkRejectedAsync(Guid reprocessamentoId, string reason, CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var reprocessamento = await _reprocessamentoRepository.GetByIdForUpdateAsync(
            reprocessamentoId,
            cancellationToken);
        if (reprocessamento is not null && !reprocessamento.IsFinal())
        {
            reprocessamento.Reject(reason, DateTime.Now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        _metrics?.RecordReprocessRequestProcessed("rejected");
        _logger.LogWarning(
            "Reprocessamento rejeitado. reprocessamentoId={ReprocessamentoId} motivo={Motivo}",
            reprocessamentoId,
            reason);
    }

    private async Task MarkFailedAsync(Guid reprocessamentoId, string reason, CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var reprocessamento = await _reprocessamentoRepository.GetByIdForUpdateAsync(
            reprocessamentoId,
            cancellationToken);
        if (reprocessamento is not null && !reprocessamento.IsFinal())
        {
            reprocessamento.Fail(reason, DateTime.Now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        _metrics?.RecordReprocessRequestProcessed("failed");
    }

    private static LedgerEntryCreatedV1 ToLedgerEntryCreated(LedgerEntry entry)
        => new(
            $"lan_{entry.Id.ToString("N")[..8]}",
            entry.Type == LedgerEntryType.Credit ? "CREDIT" : "DEBIT",
            entry.Amount.ToString("0.00", CultureInfo.InvariantCulture),
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
