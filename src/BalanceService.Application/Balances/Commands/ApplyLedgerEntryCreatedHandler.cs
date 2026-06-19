using System.Diagnostics;
using System.Globalization;

using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Application.Common.Observability;
using BalanceService.Domain.Balances;

using MediatR;

using Microsoft.Extensions.Logging;

namespace BalanceService.Application.Balances.Commands;

public sealed partial class ApplyLedgerEntryCreatedHandler : IRequestHandler<ApplyLedgerEntryCreatedCommand, ApplyLedgerEntryCreatedResult>
{
    private static readonly ActivitySource _activitySource = new("BalanceService.Application");

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Evento ja processado (idempotencia). Nenhuma alteracao aplicada.")]
    private static partial void LogDuplicateEvent(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Saldo diario consolidado atualizado (credits={TotalCredits}, debits={TotalDebits}, net={Net})")]
    private static partial void LogDailyBalanceUpdated(ILogger logger, decimal totalCredits, decimal totalDebits, decimal net);

    private readonly IDailyBalanceRepository _dailyBalanceRepository;
    private readonly IProcessedEventRepository _processedEventRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<ApplyLedgerEntryCreatedHandler> _logger;
    private readonly BalanceDomainMetrics? _metrics;

    public ApplyLedgerEntryCreatedHandler(
        IDailyBalanceRepository dailyBalanceRepository,
        IProcessedEventRepository processedEventRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<ApplyLedgerEntryCreatedHandler> logger,
        BalanceDomainMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(dailyBalanceRepository);
        ArgumentNullException.ThrowIfNull(processedEventRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _dailyBalanceRepository = dailyBalanceRepository;
        _processedEventRepository = processedEventRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<ApplyLedgerEntryCreatedResult> Handle(
        ApplyLedgerEntryCreatedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Event);

        var startedAt = Stopwatch.GetTimestamp();
        var evt = command.Event;

        try
        {
            // Importante: a consolidacao diaria deve usar o "dia" derivado do occurredAt no fuso recebido.
            // Por isso, calculamos a DateOnly ANTES de normalizar timestamps para UTC.
            var date = DateOnly.FromDateTime(evt.OccurredAt.Date);
            var currency = evt.Currency ?? throw new InvalidOperationException("LedgerEntryCreated event currency is required.");
            var now = _clock.UtcNow;

            // Npgsql + timestamptz: DateTimeOffset precisa estar em UTC (Offset=0)
            // para ser persistido. Mantemos a logica do "dia" usando o offset original.
            var occurredAtUtc = evt.OccurredAt.ToUniversalTime();
            var createdAtUtc = evt.CreatedAt.ToUniversalTime();
            var normalizedEvent = evt with
            {
                OccurredAt = occurredAtUtc,
                CreatedAt = createdAtUtc,
                Currency = currency
            };

            using var logScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["EventId"] = evt.Id,
                ["MerchantId"] = evt.MerchantId,
                ["OccurredAt"] = evt.OccurredAt,
                ["CorrelationId"] = evt.CorrelationId,
                ["BalanceDate"] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["Currency"] = currency
            });

            using var activity = _activitySource.StartActivity("balance.apply", ActivityKind.Internal);
            activity?.SetTag("balance.event_id", evt.Id);
            activity?.SetTag("balance.merchant_id", evt.MerchantId);
            activity?.SetTag("balance.occurred_at", evt.OccurredAt.ToString("o", CultureInfo.InvariantCulture));
            activity?.SetTag("correlation_id", evt.CorrelationId);
            activity?.SetTag("balance.date", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            activity?.SetTag("balance.currency", currency);
            activity?.AddBaggage("correlation_id", evt.CorrelationId);

            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var processedEvent = new ProcessedEvent(evt.Id, evt.MerchantId, occurredAtUtc, now);
            var inserted = await _processedEventRepository.TryInsertAsync(processedEvent, cancellationToken);

            if (!inserted)
            {
                LogDuplicateEvent(_logger);
                await transaction.CommitAsync(cancellationToken);
                RecordApplyMetrics(startedAt, command.EventType, "duplicate", projectionUpdated: false, currency);
                return ApplyLedgerEntryCreatedResult.IgnoredDuplicate;
            }

            await _dailyBalanceRepository.LockByMerchantDateAndCurrencyAsync(
                evt.MerchantId,
                date,
                currency,
                cancellationToken);

            var dailyBalance = await _dailyBalanceRepository.GetByMerchantDateAndCurrencyAsync(
                evt.MerchantId,
                date,
                currency,
                cancellationToken);

            if (dailyBalance is null)
            {
                dailyBalance = new DailyBalance(evt.MerchantId, date, currency, now);
                await _dailyBalanceRepository.AddAsync(dailyBalance, cancellationToken);
            }

            dailyBalance.Apply(normalizedEvent, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            RecordApplyMetrics(startedAt, command.EventType, "success", projectionUpdated: true, currency);

            LogDailyBalanceUpdated(
                _logger,
                dailyBalance.TotalCredits,
                dailyBalance.TotalDebits,
                dailyBalance.NetBalance);

            return ApplyLedgerEntryCreatedResult.Processed;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            RecordApplyMetrics(startedAt, command.EventType, "failed", projectionUpdated: false, command.Event.Currency ?? "unknown");
            throw;
        }
    }

    private void RecordApplyMetrics(long startedAt, string eventType, string result, bool projectionUpdated, string currency)
    {
        _metrics?.RecordEventApplied(eventType, result);
        _metrics?.RecordApplyDuration(
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            eventType,
            result);

        if (result == "duplicate")
            _metrics?.RecordEventDuplicate(eventType);

        if (projectionUpdated)
            _metrics?.RecordProjectionUpdated(currency);
    }
}
