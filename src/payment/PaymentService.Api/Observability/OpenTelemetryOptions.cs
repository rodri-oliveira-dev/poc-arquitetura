namespace PaymentService.Api.Observability;

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    public bool Enabled
    {
        get; init;
    }

    public string ServiceName { get; init; } = "PaymentService.Api";

    public bool UseConsoleExporter
    {
        get; init;
    }

    public string? OtlpEndpoint
    {
        get; init;
    }
}
