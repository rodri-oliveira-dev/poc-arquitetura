using System.Globalization;
using System.Text.Json;

using LedgerService.Application.Abstractions.Messaging;
using LedgerService.Application.Abstractions.Time;
using LedgerService.Application.Common.Exceptions;
using LedgerService.Application.Common.Observability;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Application.Lancamentos.Services;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Exceptions;
using LedgerService.Domain.Policies;
using LedgerService.Domain.Repositories;

using MediatR;

using Microsoft.Extensions.Logging;

namespace LedgerService.Application.Lancamentos.Commands;

public sealed partial class ProcessarEstornoLancamentoHandler : IRequestHandler<ProcessarEstornoLancamentoCommand>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Falha tecnica ao processar estorno {EstornoId}")]
    private static partial void LogTechnicalFailure(ILogger logger, Exception exception, Guid estornoId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Estorno processado. estornoId={EstornoId} lancamentoOriginalId={LancamentoOriginalId} lancamentoCompensatorioId={LancamentoCompensatorioId} statusAnterior={StatusAnterior} statusNovo={StatusNovo}")]
    private static partial void LogEstornoProcessed(
        ILogger logger,
        Guid estornoId,
        Guid lancamentoOriginalId,
        Guid lancamentoCompensatorioId,
        EstornoLancamentoStatus statusAnterior,
        EstornoLancamentoStatus statusNovo);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Estorno rejeitado. estornoId={EstornoId} motivo={Motivo}")]
    private static partial void LogEstornoRejected(ILogger logger, Guid estornoId, string motivo);

    private readonly IEstornoLancamentoRepository _estornoRepository;
    private readonly ILedgerEntryRepository _ledgerEntryRepository;
    private readonly IOutboxMessageRepository _outboxMessageRepository;
    private readonly LedgerReversalPolicy _reversalPolicy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessarEstornoLancamentoHandler> _logger;
    private readonly IClock _clock;
    private readonly LedgerDomainMetrics? _metrics;

    public ProcessarEstornoLancamentoHandler(
        IEstornoLancamentoRepository estornoRepository,
        ILedgerEntryRepository ledgerEntryRepository,
        IOutboxMessageRepository outboxMessageRepository,
        LedgerReversalPolicy reversalPolicy,
        IUnitOfWork unitOfWork,
        ILogger<ProcessarEstornoLancamentoHandler> logger,
        IClock? clock = null,
        LedgerDomainMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(estornoRepository);
        ArgumentNullException.ThrowIfNull(ledgerEntryRepository);
        ArgumentNullException.ThrowIfNull(outboxMessageRepository);
        ArgumentNullException.ThrowIfNull(reversalPolicy);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);

        _estornoRepository = estornoRepository;
        _ledgerEntryRepository = ledgerEntryRepository;
        _outboxMessageRepository = outboxMessageRepository;
        _reversalPolicy = reversalPolicy;
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
            LogTechnicalFailure(_logger, ex, request.EstornoId);
        }
    }

    private async Task ProcessInternalAsync(Guid estornoId, CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var estorno = await _estornoRepository.GetByIdForUpdateAsync(estornoId, cancellationToken) ?? throw new NotFoundException("Solicitacao de estorno nao encontrada.");

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
        var now = _clock.UtcNow.UtcDateTime;
        estorno.MarkProcessing(now);

        var lancamentoOriginal = await _ledgerEntryRepository.GetByIdAsync(estorno.LancamentoOriginalId, cancellationToken) ?? throw new NotFoundException("Lancamento original nao encontrado.");

        await _reversalPolicy.EnsureCanCompleteReversalAsync(estorno, cancellationToken);

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
            LedgerEntryCreatedV2.EventType,
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

        LogEstornoProcessed(
            _logger,
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
            estorno.Reject(reason, _clock.UtcNow.UtcDateTime);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        _metrics?.RecordReversalProcessed("rejected");
        LogEstornoRejected(_logger, estornoId, reason);
    }

    private async Task MarkFailedAsync(Guid estornoId, string reason, CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var estorno = await _estornoRepository.GetByIdForUpdateAsync(estornoId, cancellationToken);
        if (estorno is not null && !estorno.IsCompleted())
        {
            estorno.Fail(reason, _clock.UtcNow.UtcDateTime);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        _metrics?.RecordReversalProcessed("failed");
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

    private static string ToMetricResult(EstornoLancamentoStatus status)
        => status switch
        {
            EstornoLancamentoStatus.Completed => "completed",
            EstornoLancamentoStatus.Rejected => "rejected",
            EstornoLancamentoStatus.Failed => "failed",
            EstornoLancamentoStatus.Pending => throw new NotImplementedException(),
            EstornoLancamentoStatus.Processing => throw new NotImplementedException(),
            _ => "failed"
        };
}
