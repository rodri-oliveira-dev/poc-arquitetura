namespace LedgerService.Infrastructure.Estornos;

public sealed class EstornoProcessingOptions
{
    public const string SectionName = "Estornos:Processor";

    public int PollingIntervalSeconds { get; init; } = 5;
    public int BatchSize { get; init; } = 10;
}
