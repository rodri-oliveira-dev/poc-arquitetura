using System.Diagnostics.Metrics;

namespace PaymentService.Worker.Observability;

public sealed class PaymentInboxWorkerMetrics : IDisposable
{
    public const string MeterName = "PaymentService.InboxWorker";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _claimTotal;
    private readonly Counter<long> _processTotal;
    private readonly Counter<long> _failureTotal;
    private readonly Counter<long> _retryScheduledTotal;
    private readonly Counter<long> _deadLetterTotal;
    private readonly Counter<long> _regressiveEventTotal;
    private readonly Counter<long> _idempotentTransitionTotal;
    private readonly Counter<long> _recoveredLeaseTotal;
    private readonly Histogram<double> _processingDuration;
    private int _lastBacklog;

    public PaymentInboxWorkerMetrics()
    {
        _claimTotal = _meter.CreateCounter<long>("payment_inbox_claim_total");
        _processTotal = _meter.CreateCounter<long>("payment_inbox_process_total");
        _failureTotal = _meter.CreateCounter<long>("payment_inbox_process_failure_total");
        _retryScheduledTotal = _meter.CreateCounter<long>("payment_inbox_retry_scheduled_total");
        _deadLetterTotal = _meter.CreateCounter<long>("payment_inbox_deadletter_total");
        _regressiveEventTotal = _meter.CreateCounter<long>("payment_inbox_regressive_event_total");
        _idempotentTransitionTotal = _meter.CreateCounter<long>("payment_inbox_idempotent_transition_total");
        _recoveredLeaseTotal = _meter.CreateCounter<long>("payment_inbox_recovered_lease_total");
        _processingDuration = _meter.CreateHistogram<double>("payment_inbox_processing_duration", unit: "ms");
        _meter.CreateObservableGauge("payment_inbox_backlog", () => _lastBacklog);
    }

    public void RecordClaim(int count, int recoveredLeaseCount)
    {
        _claimTotal.Add(count, new KeyValuePair<string, object?>("provider", "Stripe"));
        if (recoveredLeaseCount > 0)
            _recoveredLeaseTotal.Add(recoveredLeaseCount, new KeyValuePair<string, object?>("provider", "Stripe"));
    }

    public void RecordProcess(string outcome, double elapsedMilliseconds)
    {
        _processTotal.Add(1, new KeyValuePair<string, object?>("provider", "Stripe"), new KeyValuePair<string, object?>("outcome", outcome));
        _processingDuration.Record(elapsedMilliseconds, new KeyValuePair<string, object?>("provider", "Stripe"), new KeyValuePair<string, object?>("outcome", outcome));

        if (string.Equals(outcome, "retry_scheduled", StringComparison.Ordinal))
            _retryScheduledTotal.Add(1, new KeyValuePair<string, object?>("provider", "Stripe"));
        else if (string.Equals(outcome, "dead_letter", StringComparison.Ordinal))
            _deadLetterTotal.Add(1, new KeyValuePair<string, object?>("provider", "Stripe"));
        else if (string.Equals(outcome, "regressive_ignored", StringComparison.Ordinal))
            _regressiveEventTotal.Add(1, new KeyValuePair<string, object?>("provider", "Stripe"));
        else if (string.Equals(outcome, "idempotent", StringComparison.Ordinal))
            _idempotentTransitionTotal.Add(1, new KeyValuePair<string, object?>("provider", "Stripe"));
    }

    public void RecordFailure(string errorCategory)
        => _failureTotal.Add(1, new KeyValuePair<string, object?>("provider", "Stripe"), new KeyValuePair<string, object?>("error_category", errorCategory));

    public void SetBacklog(int backlog)
        => _lastBacklog = Math.Max(0, backlog);

    public void Dispose()
    {
        _meter.Dispose();
    }
}
