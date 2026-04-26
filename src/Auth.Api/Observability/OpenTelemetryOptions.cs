namespace Auth.Api.Observability;

/// <summary>
/// Configurações de observabilidade (OpenTelemetry).
/// </summary>
public sealed class OpenTelemetryOptions
{
    public const string SectionName = "Observability:OpenTelemetry";

    /// <summary>
    /// Habilita OpenTelemetry (traces e metricas) na aplicacao.
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Quando <see cref="Enabled"/> for true, exporta traces e metricas para o console.
    /// Util para validacao local sem depender de backend OTLP.
    /// </summary>
    public bool UseConsoleExporter { get; init; } = false;

    /// <summary>
    /// Endpoint OTLP opcional usado para exportar traces e metricas.
    /// Exemplo: http://localhost:4317.
    /// </summary>
    public string? OtlpEndpoint { get; init; }

    /// <summary>
    /// Nome do servico usado no Resource do OpenTelemetry.
    /// </summary>
    public string ServiceName { get; init; } = "Auth.Api";
}
