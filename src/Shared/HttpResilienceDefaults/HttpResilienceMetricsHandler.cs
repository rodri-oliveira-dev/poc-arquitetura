using System.Diagnostics;

using Microsoft.Extensions.Logging;

using Polly.CircuitBreaker;

namespace HttpResilienceDefaults;

internal sealed class HttpResilienceMetricsHandler : DelegatingHandler
{
    private static readonly Action<ILogger, string, string, Exception?> _logOpenCircuitRejectedCall =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(5, nameof(_logOpenCircuitRejectedCall)),
            "Chamada HTTP rejeitada por circuito aberto. Client={ClientName} Operation={Operation}");

    private readonly string _clientName;
    private readonly HttpResilienceMetrics _metrics;
    private readonly ILogger _logger;

    public HttpResilienceMetricsHandler(
        string clientName,
        HttpResilienceMetrics metrics,
        ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);

        _clientName = clientName;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string operation = HttpResilienceMetrics.GetOperation(request);
        long startedAt = Stopwatch.GetTimestamp();

        try
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            _metrics.RecordRequestDuration(
                _clientName,
                operation,
                response.IsSuccessStatusCode ? "success" : "http_error",
                Stopwatch.GetElapsedTime(startedAt));

            return response;
        }
        catch (BrokenCircuitException ex)
        {
            _logOpenCircuitRejectedCall(_logger, _clientName, operation, ex);
            _metrics.RecordOpenCircuitRejectedCall(_clientName, operation, ex);
            _metrics.RecordRequestDuration(
                _clientName,
                operation,
                "open_circuit_rejected",
                Stopwatch.GetElapsedTime(startedAt),
                ex);
            throw;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _metrics.RecordRequestDuration(
                _clientName,
                operation,
                "exception",
                Stopwatch.GetElapsedTime(startedAt),
                ex);
            throw;
        }
    }
}
