using System.Diagnostics;
using System.Diagnostics.Metrics;

using PaymentService.Application.Abstractions.Gateway;

namespace PaymentService.Infrastructure.Gateway;

public sealed class PaymentGatewayTelemetry : IDisposable
{
    public const string ActivitySourceName = "PaymentService.Infrastructure.PaymentGateway";
    public const string MeterName = "PaymentService.PaymentGateway";

    private readonly ActivitySource _activitySource = new(ActivitySourceName);
    private readonly Meter _meter;
    private readonly Counter<long> _requestCounter;
    private readonly Counter<long> _failureCounter;
    private readonly Histogram<double> _durationHistogram;

    public PaymentGatewayTelemetry(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        _meter = meterFactory.Create(MeterName);
        _requestCounter = _meter.CreateCounter<long>("payment_provider_request_total");
        _failureCounter = _meter.CreateCounter<long>("payment_provider_failure_total");
        _durationHistogram = _meter.CreateHistogram<double>("payment_provider_request_duration", "ms");
    }

    public Activity? StartCreateActivity(string provider)
    {
        var activity = _activitySource.StartActivity("payment.provider.create", ActivityKind.Client);
        activity?.SetTag("payment.provider", provider);
        activity?.SetTag("payment.operation", "create");
        return activity;
    }

    public void RecordSuccess(string provider, long elapsedMilliseconds)
    {
        TagList tags = default;
        tags.Add("provider", provider);
        tags.Add("operation", "create");
        tags.Add("outcome", "success");

        _requestCounter.Add(1, tags);
        _durationHistogram.Record(elapsedMilliseconds, tags);
    }

    public void RecordFailure(string provider, PaymentGatewayErrorCategory category, long elapsedMilliseconds)
    {
        TagList tags = default;
        tags.Add("provider", provider);
        tags.Add("operation", "create");
        tags.Add("outcome", "failure");
        tags.Add("error_category", category.ToString());

        _requestCounter.Add(1, tags);
        _failureCounter.Add(1, tags);
        _durationHistogram.Record(elapsedMilliseconds, tags);
    }

    public void Dispose()
    {
        _activitySource.Dispose();
        _meter.Dispose();
    }
}
