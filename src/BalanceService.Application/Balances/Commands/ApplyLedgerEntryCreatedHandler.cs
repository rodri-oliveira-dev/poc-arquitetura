using BalanceService.Application.Abstractions.Persistence;
using BalanceService.Application.Abstractions.Time;
using BalanceService.Domain.Balances;

using MediatR;

using Microsoft.Extensions.Logging;

using System.Diagnostics;
using System.Globalization;

namespace BalanceService.Application.Balances.Commands;

public sealed class ApplyLedgerEntryCreatedHandler : IRequestHandler<ApplyLedgerEntryCreatedCommand>
{
    private static readonly ActivitySource _activitySource = new("BalanceService.Application");

    // TODO: confirmar a origem/contrato de currency no evento. No payload atual não há currency.
    // Para não bloquear o processamento da POC, usamos um default conservador.
    private const string DefaultCurrency = "BRL";

    private readonly IDailyBalanceRepository _dailyBalanceRepository;
    private readonly IProcessedEventRepository _processedEventRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<ApplyLedgerEntryCreatedHandler> _logger;

    public ApplyLedgerEntryCreatedHandler(
        IDailyBalanceRepository dailyBalanceRepository,
        IProcessedEventRepository processedEventRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<ApplyLedgerEntryCreatedHandler> logger)
    {
        _dailyBalanceRepository = dailyBalanceRepository;
        _processedEventRepository = processedEventRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task Handle(ApplyLedgerEntryCreatedCommand command, CancellationToken cancellationToken)
    {
        var evt = command.Event;

        // Importante: a consolidação diária deve usar o "dia" derivado do occurredAt no fuso recebido.
        // Por isso, calculamos a DateOnly ANTES de normalizar timestamps para UTC.
        var date = DateOnly.FromDateTime(evt.OccurredAt.Date);
        var currency = DefaultCurrency;
        var now = _clock.UtcNow;

        // Npgsql + timestamptz: DateTimeOffset precisa estar em UTC (Offset=0)
        // para ser persistido. Mantemos a lógica do "dia" usando o offset original.
        var occurredAtUtc = evt.OccurredAt.ToUniversalTime();
        var createdAtUtc = evt.CreatedAt.ToUniversalTime();
        var normalizedEvent = evt with
        {
            OccurredAt = occurredAtUtc,
            CreatedAt = createdAtUtc
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
        activity?.SetTag("messaging.system", "kafka");
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
            _logger.LogDebug("Evento já processado (idempotência). Nenhuma alteração aplicada.");
            await transaction.CommitAsync(cancellationToken);
            return;
        }

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

        _logger.LogDebug(
            "Saldo diário consolidado atualizado (credits={TotalCredits}, debits={TotalDebits}, net={Net})",
            dailyBalance.TotalCredits,
            dailyBalance.TotalDebits,
            dailyBalance.NetBalance);
    }
}
