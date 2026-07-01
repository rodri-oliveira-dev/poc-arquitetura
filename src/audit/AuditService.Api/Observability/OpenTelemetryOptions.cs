namespace AuditService.Api.Observability;

/// <summary>
/// Configuracoes de observabilidade (OpenTelemetry).
/// </summary>
public sealed class OpenTelemetryOptions
{
    public const string SectionName = "Observability:OpenTelemetry";

    /// <summary>
    /// Habilita OpenTelemetry (traces/metricas) na aplicacao.
    /// </summary>
    public bool Enabled
    {
        get; init;
    }

    /// <summary>
    /// Quando habilitado, exporta traces e metricas para o console.
    /// </summary>
    public bool UseConsoleExporter
    {
        get; init;
    }

    /// <summary>
    /// Endpoint OTLP opcional usado para exportar traces e metricas.
    /// </summary>
    public string? OtlpEndpoint
    {
        get; init;
    }

    /// <summary>
    /// Nome do servico usado no Resource do OpenTelemetry.
    /// </summary>
    public string ServiceName { get; init; } = "AuditService.Api";
}
