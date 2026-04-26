namespace BalanceService.Api.Observability;

/// <summary>
/// Configurações de observabilidade (OpenTelemetry).
/// </summary>
public sealed class OpenTelemetryOptions
{
    public const string SectionName = "Observability:OpenTelemetry";

    /// <summary>
    /// Habilita OpenTelemetry (traces/métricas) na aplicação.
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Quando <see cref="Enabled"/> for true, exporta traces e métricas para o console.
    /// Útil para validação local sem depender de backend (Jaeger/Tempo/Prometheus etc.).
    /// </summary>
    public bool UseConsoleExporter { get; init; } = false;

    /// <summary>
    /// Endpoint OTLP opcional usado para exportar traces e métricas.
    /// Exemplo: http://localhost:4317.
    /// </summary>
    public string? OtlpEndpoint { get; init; }

    /// <summary>
    /// Nome do serviço usado no Resource do OpenTelemetry.
    /// </summary>
    public string ServiceName { get; init; } = "BalanceService.Api";
}
