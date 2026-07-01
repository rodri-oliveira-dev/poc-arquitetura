namespace BalanceService.Worker.Observability;

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "Observability:OpenTelemetry";

    public bool Enabled
    {
        get; init;
    }
    public bool UseConsoleExporter
    {
        get; init;
    }
    public string OtlpEndpoint { get; init; } = string.Empty;
    public string ServiceName { get; init; } = "BalanceService.Worker";
}
