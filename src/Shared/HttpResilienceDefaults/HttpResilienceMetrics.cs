using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HttpResilienceDefaults;

public sealed class HttpResilienceMetrics : IDisposable
{
    public const string MeterName = "HttpResilienceDefaults";

    private readonly Meter _meter;
    private readonly Counter<long> _retries;
    private readonly Counter<long> _timeouts;
    private readonly Counter<long> _circuitBreakerOpened;
    private readonly Counter<long> _circuitBreakerHalfOpened;
    private readonly Counter<long> _circuitBreakerClosed;
    private readonly Counter<long> _openCircuitRejectedCalls;
    private readonly Histogram<double> _requestDuration;

    public HttpResilienceMetrics()
        : this(MeterName)
    {
    }

    public HttpResilienceMetrics(string meterName)
    {
        _meter = new Meter(meterName);
        _retries = _meter.CreateCounter<long>(
            "http.resilience.retries",
            unit: "1",
            description: "Total de retries executados por politicas HTTP resilientes.");
        _timeouts = _meter.CreateCounter<long>(
            "http.resilience.timeouts",
            unit: "1",
            description: "Total de timeouts observados por politicas HTTP resilientes.");
        _circuitBreakerOpened = _meter.CreateCounter<long>(
            "http.resilience.circuit_breaker.opened",
            unit: "1",
            description: "Total de transicoes do circuit breaker HTTP para open.");
        _circuitBreakerHalfOpened = _meter.CreateCounter<long>(
            "http.resilience.circuit_breaker.half_opened",
            unit: "1",
            description: "Total de transicoes do circuit breaker HTTP para half-open.");
        _circuitBreakerClosed = _meter.CreateCounter<long>(
            "http.resilience.circuit_breaker.closed",
            unit: "1",
            description: "Total de transicoes do circuit breaker HTTP para closed.");
        _openCircuitRejectedCalls = _meter.CreateCounter<long>(
            "http.resilience.open_circuit.rejected_calls",
            unit: "1",
            description: "Total de chamadas HTTP rejeitadas por circuito aberto.");
        _requestDuration = _meter.CreateHistogram<double>(
            "http.resilience.request.duration",
            unit: "s",
            description: "Duracao de chamadas HTTP protegidas pelas politicas resilientes.");
    }

    public void RecordRetry(string client, string operation, Exception? exception)
        => _retries.Add(1, CreateTags(client, operation, "retry", exception));

    public void RecordTimeout(string client, string operation)
        => _timeouts.Add(1, CreateTags(client, operation, "timeout", exceptionType: "TimeoutRejectedException"));

    public void RecordCircuitOpened(string client, string operation, Exception? exception)
        => _circuitBreakerOpened.Add(1, CreateTags(client, operation, "open", exception));

    public void RecordCircuitHalfOpened(string client, string operation)
        => _circuitBreakerHalfOpened.Add(1, CreateTags(client, operation, "half_open"));

    public void RecordCircuitClosed(string client, string operation)
        => _circuitBreakerClosed.Add(1, CreateTags(client, operation, "closed"));

    public void RecordOpenCircuitRejectedCall(string client, string operation, Exception exception)
        => _openCircuitRejectedCalls.Add(1, CreateTags(client, operation, "open_circuit_rejected", exception));

    public void RecordRequestDuration(string client, string operation, string outcome, TimeSpan duration, Exception? exception = null)
        => _requestDuration.Record(duration.TotalSeconds, CreateTags(client, operation, outcome, exception));

    public void Dispose()
        => _meter.Dispose();

    internal static string GetDependency(string client)
        => client switch
        {
            "Ledger" => "LedgerService.Api",
            "Keycloak" => "Keycloak",
            "JWKS" => "JWKS",
            _ => client
        };

    internal static string GetOperation(HttpRequestMessage request)
        => request.Method.Method;

    private static TagList CreateTags(
        string client,
        string operation,
        string outcome,
        Exception? exception = null,
        string? exceptionType = null)
    {
        TagList tags = new()
        {
            { "client", client },
            { "dependency", GetDependency(client) },
            { "operation", operation },
            { "outcome", outcome }
        };

        string? stableExceptionType = exceptionType ?? exception?.GetType().Name;
        if (!string.IsNullOrWhiteSpace(stableExceptionType))
        {
            tags.Add("exception_type", stableExceptionType);
        }

        return tags;
    }
}
