using System.Diagnostics.Metrics;

namespace LedgerService.Application.Common.Observability;

public sealed class LedgerDomainMetrics : IDisposable
{
    public const string MeterName = "LedgerService.Domain";
    public const string EntriesCreatedMetricName = "ledger.entries.created";
    public const string EntriesRejectedMetricName = "ledger.entries.rejected";
    public const string ReversalsRequestedMetricName = "ledger.reversals.requested";
    public const string ReversalsProcessedMetricName = "ledger.reversals.processed";
    public const string ReprocessRequestsCreatedMetricName = "ledger.reprocess.requests.created";
    public const string ReprocessRequestsProcessedMetricName = "ledger.reprocess.requests.processed";
    public const string IdempotencyHitsMetricName = "ledger.idempotency.hits";

    private readonly Meter _meter;
    private readonly Counter<long> _entriesCreated;
    private readonly Counter<long> _entriesRejected;
    private readonly Counter<long> _reversalsRequested;
    private readonly Counter<long> _reversalsProcessed;
    private readonly Counter<long> _reprocessRequestsCreated;
    private readonly Counter<long> _reprocessRequestsProcessed;
    private readonly Counter<long> _idempotencyHits;

    public LedgerDomainMetrics()
        : this(MeterName)
    {
    }

    public LedgerDomainMetrics(string meterName)
    {
        _meter = new Meter(meterName);
        _entriesCreated = _meter.CreateCounter<long>(
            EntriesCreatedMetricName,
            unit: "1",
            description: "Total de lancamentos financeiros criados pelo Ledger por tipo, moeda e resultado.");
        _entriesRejected = _meter.CreateCounter<long>(
            EntriesRejectedMetricName,
            unit: "1",
            description: "Total de lancamentos rejeitados pelo Ledger por classificacao estavel.");
        _reversalsRequested = _meter.CreateCounter<long>(
            ReversalsRequestedMetricName,
            unit: "1",
            description: "Total de solicitacoes de estorno aceitas pelo Ledger por resultado.");
        _reversalsProcessed = _meter.CreateCounter<long>(
            ReversalsProcessedMetricName,
            unit: "1",
            description: "Total de solicitacoes de estorno processadas pelo Ledger por resultado final.");
        _reprocessRequestsCreated = _meter.CreateCounter<long>(
            ReprocessRequestsCreatedMetricName,
            unit: "1",
            description: "Total de solicitacoes de reprocessamento aceitas pelo Ledger por resultado.");
        _reprocessRequestsProcessed = _meter.CreateCounter<long>(
            ReprocessRequestsProcessedMetricName,
            unit: "1",
            description: "Total de solicitacoes de reprocessamento processadas pelo Ledger por resultado final.");
        _idempotencyHits = _meter.CreateCounter<long>(
            IdempotencyHitsMetricName,
            unit: "1",
            description: "Total de replays atendidos por idempotencia no Ledger por operacao.");
    }

    public void RecordEntryCreated(string entryType, string currency, string result)
    {
        _entriesCreated.Add(
            1,
            new KeyValuePair<string, object?>("entry_type", entryType),
            new KeyValuePair<string, object?>("currency", currency),
            new KeyValuePair<string, object?>("result", result));
    }

    public void RecordEntryRejected(string reason)
    {
        _entriesRejected.Add(
            1,
            new KeyValuePair<string, object?>("reason", reason));
    }

    public void RecordReversalRequested(string result)
    {
        _reversalsRequested.Add(
            1,
            new KeyValuePair<string, object?>("result", result));
    }

    public void RecordReversalProcessed(string result)
    {
        _reversalsProcessed.Add(
            1,
            new KeyValuePair<string, object?>("result", result));
    }

    public void RecordReprocessRequestCreated(string result)
    {
        _reprocessRequestsCreated.Add(
            1,
            new KeyValuePair<string, object?>("result", result));
    }

    public void RecordReprocessRequestProcessed(string result)
    {
        _reprocessRequestsProcessed.Add(
            1,
            new KeyValuePair<string, object?>("result", result));
    }

    public void RecordIdempotencyHit(string operation)
    {
        _idempotencyHits.Add(
            1,
            new KeyValuePair<string, object?>("operation", operation));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
