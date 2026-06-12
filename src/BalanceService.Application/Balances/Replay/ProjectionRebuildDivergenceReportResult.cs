using System.Text.Json.Serialization;

namespace BalanceService.Application.Balances.Replay;

public sealed record ProjectionRebuildDivergenceReportResult
{
    public ProjectionRebuildDivergenceReportResult(
        string reportId,
        bool mutated,
        string filterDescription,
        ProjectionRebuildDivergenceSummary summary,
        IReadOnlyList<ProjectionRebuildDivergenceItem> items,
        IReadOnlyList<ProjectionRebuildEventItemResult> events)
    {
        ReportId = reportId;
        Mutated = mutated;
        FilterDescription = filterDescription;
        Summary = summary;
        Items = items;
        Events = events;
    }

    public string ReportId { get; init; }
    public bool Mutated { get; init; }
    public string FilterDescription { get; init; }

    [JsonIgnore]
    public ProjectionRebuildDivergenceSummary Summary { get; init; }

    public int TotalFound => Summary.TotalFound;
    public int TotalValid => Summary.TotalValid;
    public int TotalInvalid => Summary.TotalInvalid;
    public int TotalDuplicates => Summary.TotalDuplicates;
    public int TotalCompared => Summary.TotalCompared;
    public bool HasDivergences => Summary.HasDivergences;
    public IReadOnlyList<ProjectionRebuildDivergenceItem> Items { get; init; }
    public IReadOnlyList<ProjectionRebuildEventItemResult> Events { get; init; }
}
