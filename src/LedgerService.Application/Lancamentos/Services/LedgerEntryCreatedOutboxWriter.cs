using System.Text.Json;

using LedgerService.Application.Common.Models;
using LedgerService.Application.Common.Observability;
using LedgerService.Application.Lancamentos.Events;
using LedgerService.Domain.Entities;
using LedgerService.Domain.Repositories;

namespace LedgerService.Application.Lancamentos.Services;

public sealed class LedgerEntryCreatedOutboxWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IOutboxMessageRepository _outboxMessageRepository;

    public LedgerEntryCreatedOutboxWriter(IOutboxMessageRepository outboxMessageRepository)
    {
        ArgumentNullException.ThrowIfNull(outboxMessageRepository);

        _outboxMessageRepository = outboxMessageRepository;
    }

    public Task WriteAsync(
        LedgerEntry ledgerEntry,
        LancamentoDto response,
        string correlationId,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ledgerEntry);

        var outboxPayload = JsonSerializer.Serialize(
            LedgerEntryCreatedEventFactory.Create(response, correlationId),
            JsonOptions);

        var traceContext = OutboxTraceContext.CaptureCurrent();
        var outboxMessage = new OutboxMessage(
            "LedgerEntry",
            ledgerEntry.Id,
            LedgerEntryCreatedV1.EventType,
            outboxPayload,
            occurredAt,
            ledgerEntry.CorrelationId,
            traceContext.TraceParent,
            traceContext.TraceState,
            traceContext.Baggage);

        return _outboxMessageRepository.AddAsync(outboxMessage, cancellationToken);
    }
}
