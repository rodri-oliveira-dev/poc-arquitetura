using System.Diagnostics.Metrics;

namespace PaymentService.Worker.Observability;

public sealed class PaymentLedgerWorkerMetrics : IDisposable
{
    public const string MeterName = "PaymentService.LedgerWorker";
    private const string OperationTagName = "operation";
    private const string CreditOperation = "credit";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _claimTotal;
    private readonly Counter<long> _requestTotal;
    private readonly Counter<long> _successTotal;
    private readonly Counter<long> _failureTotal;
    private readonly Counter<long> _retryScheduledTotal;
    private readonly Counter<long> _deadLetterTotal;
    private readonly Histogram<double> _processingDuration;

    public PaymentLedgerWorkerMetrics()
    {
        _claimTotal = _meter.CreateCounter<long>("payment_ledger_claim_total");
        _requestTotal = _meter.CreateCounter<long>("payment_ledger_request_total");
        _successTotal = _meter.CreateCounter<long>("payment_ledger_success_total");
        _failureTotal = _meter.CreateCounter<long>("payment_ledger_failure_total");
        _retryScheduledTotal = _meter.CreateCounter<long>("payment_ledger_retry_scheduled_total");
        _deadLetterTotal = _meter.CreateCounter<long>("payment_ledger_deadletter_total");
        _processingDuration = _meter.CreateHistogram<double>("payment_ledger_processing_duration", unit: "ms");
    }

    public void RecordBatch(PaymentLedgerWorkerBatchMetrics batch, double elapsedMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(batch);

        _claimTotal.Add(batch.Claimed, new KeyValuePair<string, object?>(OperationTagName, CreditOperation));
        _requestTotal.Add(batch.Claimed, new KeyValuePair<string, object?>(OperationTagName, CreditOperation));
        _successTotal.Add(batch.Completed, new KeyValuePair<string, object?>(OperationTagName, CreditOperation));
        _retryScheduledTotal.Add(batch.RetryScheduled, new KeyValuePair<string, object?>(OperationTagName, CreditOperation));
        _deadLetterTotal.Add(batch.DeadLettered, new KeyValuePair<string, object?>(OperationTagName, CreditOperation));

        if (batch.FailedDefinitive > 0)
            _failureTotal.Add(batch.FailedDefinitive, new KeyValuePair<string, object?>(OperationTagName, CreditOperation), new KeyValuePair<string, object?>("error_category", "definitive"));

        _processingDuration.Record(elapsedMilliseconds, new KeyValuePair<string, object?>(OperationTagName, CreditOperation));
    }

    public void RecordFailure(string errorCategory)
        => _failureTotal.Add(1, new KeyValuePair<string, object?>(OperationTagName, CreditOperation), new KeyValuePair<string, object?>("error_category", errorCategory));

    public void Dispose()
        => _meter.Dispose();
}

public sealed record PaymentLedgerWorkerBatchMetrics(
    int Claimed,
    int Completed,
    int RetryScheduled,
    int FailedDefinitive,
    int DeadLettered);
