using System.Text.Json.Serialization;

namespace BalanceService.Application.Balances.Replay;

public sealed record FilteredEventReplayResult
{
    public FilteredEventReplayResult(
        string replayId,
        bool dryRun,
        FilteredEventReplaySummary summary,
        IReadOnlyList<FilteredEventReplayItemResult> items)
    {
        ReplayId = replayId;
        DryRun = dryRun;
        Summary = summary;
        Items = items;
    }

    public string ReplayId
    {
        get; init;
    }
    public bool DryRun
    {
        get; init;
    }

    [JsonIgnore]
    public FilteredEventReplaySummary Summary
    {
        get; init;
    }

    public int TotalFound => Summary.TotalFound;
    public int TotalValid => Summary.TotalValid;
    public int TotalInvalid => Summary.TotalInvalid;
    public int TotalAlreadyProcessed => Summary.TotalAlreadyProcessed;
    public int TotalEligible => Summary.TotalEligible;
    public int TotalRejected => Summary.TotalRejected;
    public int TotalReplayed => Summary.TotalReplayed;
    public IReadOnlyList<FilteredEventReplayItemResult> Items
    {
        get; init;
    }
}
