namespace Auth.Api.Observability;

/// <summary>
/// Configurações de observabilidade (OpenTelemetry).
/// </summary>
public sealed class OpenTelemetryOptions
{
    public const string SectionName = "Observability:OpenTelemetry";

    public bool Enabled { get; init; } = false;
    public bool UseConsoleExporter { get; init; } = false;
    public string ServiceName { get; init; } = "Auth.Api";
}
