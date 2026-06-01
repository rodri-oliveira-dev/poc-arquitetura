using System.Globalization;
using System.Text.Json;

using LedgerService.Application.Abstractions.Time;
using LedgerService.Application.Common.Exceptions;
using LedgerService.Application.Common.Observability;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Exceptions;
using LedgerService.Domain.Repositories;

using MediatR;

using Microsoft.Extensions.Logging;

namespace LedgerService.Application.Lancamentos.Commands;

public sealed class ProcessarEstornoLancamentoHandler : IRequestHandler<ProcessarEstornoLancamentoCommand>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IEstornoLancamentoRepository _estornoRepository;
    private readonly ILedgerEntryRepository _ledgerEntryRepository;
    private readonly IOutboxMessageRepository _outboxMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessarEstornoLancamentoHandler> _logger;
    private readonly IClock _clock;
    private readonly LedgerDomainMetrics? _metrics;

    public ProcessarEstornoLancamentoHandler(
        IEstornoLancamentoRepository estornoRepository,
        ILedgerEntryRepository ledgerEntryRepository,
        IOutboxMessageRepository outboxMessageRepository,
        IUnitOfWork unitOfWork,
        ILogger<ProcessarEstornoLancamentoHandler> logger,
        IClock? clock = null,
        LedgerDomainMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(estornoRepository);
        ArgumentNullException.ThrowIfNull(ledgerEntryRepository);
        ArgumentNullException.ThrowIfNull(outboxMessageRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);

        _estornoRepository = estornoRepository;
        _ledgerEntryRepository = ledgerEntryRepository;
        _outboxMessageRepository = outboxMessageRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _clock = clock ?? new SystemClock();
        _metrics = metrics;
    }

    public async Task Handle(ProcessarEstornoLancamentoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            await ProcessInternalAsync(request.EstornoId, cancellationToken);
        }
        catch (DomainException ex)
        {
            await MarkRejectedAsync(request.EstornoId, ex.Message, cancellationToken);
        }
        catch (NotFoundException ex)
        {
            await MarkRejectedAsync(request.EstornoId, ex.Message, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await MarkFailedAsync(request.EstornoId, "Falha tecnica ao processar estorno.", cancellationToken);
            _logger.LogError(ex, "Falha tecnica ao processar estorno {EstornoId}", request.EstornoId);
        }
    }

    private async Task ProcessInternalAsync(Guid estornoId, CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var estorno = await _estornoRepository.GetByIdForUpdateAsync(estornoId, cancellationToken);
        if (estorno is null)
            throw new NotFoundException("Solicitacao de estorno nao encontrada.");

        if (estorno.IsCompleted())
        {
            await transaction.CommitAsync(cancellationToken);
            _metrics?.RecordReversalProcessed("completed");
            return;
        }

        if (estorno.Status is EstornoLancamentoStatus.Rejected or EstornoLancamentoStatus.Failed)
        {
            await transaction.CommitAsync(cancellationToken);
            _metrics?.RecordReversalProcessed(ToMetricResult(estorno.Status));
            return;
        }

        var previousStatus = estorno.Status;
        var now = _clock.UtcNow.DateTime;
        estorno.MarkProcessing(now);

        var lancamentoOriginal = await _ledgerEntryRepository.GetByIdAsync(estorno.LancamentoOriginalId, cancellationToken);
        if (lancamentoOriginal is null)
            throw new NotFoundException("Lancamento original nao encontrado.");

        var completedEstorno = await _estornoRepository.GetCompletedByLancamentoOriginalIdAsync(
            estorno.LancamentoOriginalId,
            cancellationToken);

        if (completedEstorno is not null && completedEstorno.Id != estorno.Id)
            throw new DomainException("Lancamento ja foi estornado.");

        var compensatingEntry = await _ledgerEntryRepository.GetCompensatingEntryAsync(
            estorno.LancamentoOriginalId,
            cancellationToken);

        if (compensatingEntry is null)
        {
            compensatingEntry = lancamentoOriginal.CreateCompensatingEntry(estorno.CorrelationId, estorno.Motivo, now);
            await _ledgerEntryRepository.AddAsync(compensatingEntry, cancellationToken);
        }

        estorno.Complete(compensatingEntry.Id, now);

        var outboxPayload = JsonSerializer.Serialize(ToLedgerEntryCreated(compensatingEntry), JsonOptions);
        var traceContext = OutboxTraceContext.CaptureCurrent();
        var outboxMessage = new OutboxMessage(
            "LedgerEntry",
            compensatingEntry.Id,
            LedgerEntryCreatedV1.EventType,
            outboxPayload,
            now,
            estorno.CorrelationId,
            traceContext.TraceParent,
            traceContext.TraceState,
            traceContext.Baggage);

        await _outboxMessageRepository.AddAsync(outboxMessage, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _metrics?.RecordReversalProcessed("completed");

        _logger.LogInformation(
            "Estorno processado. estornoId={EstornoId} lancamentoOriginalId={LancamentoOriginalId} lancamentoCompensatorioId={LancamentoCompensatorioId} statusAnterior={StatusAnterior} statusNovo={StatusNovo}",
            estorno.Id,
            estorno.LancamentoOriginalId,
            compensatingEntry.Id,
            previousStatus,
            estorno.Status);
    }

    private async Task MarkRejectedAsync(Guid estornoId, string reason, CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var estorno = await _estornoRepository.GetByIdForUpdateAsync(estornoId, cancellationToken);
        if (estorno is not null && !estorno.IsCompleted())
        {
            estorno.Reject(reason, _clock.UtcNow.DateTime);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        _metrics?.RecordReversalProcessed("rejected");
        _logger.LogWarning("Estorno rejeitado. estornoId={EstornoId} motivo={Motivo}", estornoId, reason);
    }

    private async Task MarkFailedAsync(Guid estornoId, string reason, CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var estorno = await _estornoRepository.GetByIdForUpdateAsync(estornoId, cancellationToken);
        if (estorno is not null && !estorno.IsCompleted())
        {
            estorno.Fail(reason, _clock.UtcNow.DateTime);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        _metrics?.RecordReversalProcessed("failed");
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

    private static string ToMetricResult(EstornoLancamentoStatus status)
        => status switch
        {
            EstornoLancamentoStatus.Completed => "completed",
            EstornoLancamentoStatus.Rejected => "rejected",
            EstornoLancamentoStatus.Failed => "failed",
            _ => "failed"
        };
}
