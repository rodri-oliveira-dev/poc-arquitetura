using System.Diagnostics.Metrics;

namespace ApiDefaults.RateLimiting;

public sealed class ApiRateLimitMetrics : IDisposable
{
    public const string MeterName = "ApiDefaults.RateLimiting";

    private readonly Meter _meter;
    private readonly Counter<long> _rejectedRequests;

    public ApiRateLimitMetrics()
        : this(MeterName)
    {
    }

    internal ApiRateLimitMetrics(string meterName)
    {
        _meter = new Meter(meterName);
        _rejectedRequests = _meter.CreateCounter<long>(
            "api.rate_limiting.rejected_requests",
            unit: "{request}",
            description: "Requests rejected by API rate limiting.");
    }

    public void RecordRejected(string policy, string partitionType)
    {
        _rejectedRequests.Add(
            1,
            new KeyValuePair<string, object?>("policy", policy),
            new KeyValuePair<string, object?>("partition_type", partitionType));
    }

    public void Dispose()
        => _meter.Dispose();
}
