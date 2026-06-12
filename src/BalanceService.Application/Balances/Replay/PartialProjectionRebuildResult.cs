using System.Text.Json.Serialization;

namespace BalanceService.Application.Balances.Replay;

public sealed record PartialProjectionRebuildResult
{
    public PartialProjectionRebuildResult(
        string rebuildId,
        bool dryRun,
        bool mutated,
        string filterDescription,
        ProjectionRebuildEvaluationSummary evaluationSummary,
        ProjectionRebuildMutationSummary mutationSummary,
        IReadOnlyList<PartialProjectionRebuildItemResult> items)
    {
        RebuildId = rebuildId;
        DryRun = dryRun;
        Mutated = mutated;
        FilterDescription = filterDescription;
        EvaluationSummary = evaluationSummary;
        MutationSummary = mutationSummary;
        Items = items;
    }

    public string RebuildId { get; init; }
    public bool DryRun { get; init; }
    public bool Mutated { get; init; }
    public string FilterDescription { get; init; }

    [JsonIgnore]
    public ProjectionRebuildEvaluationSummary EvaluationSummary { get; init; }

    [JsonIgnore]
    public ProjectionRebuildMutationSummary MutationSummary { get; init; }

    public int TotalFound => EvaluationSummary.TotalFound;
    public int TotalValid => EvaluationSummary.TotalValid;
    public int TotalInvalid => EvaluationSummary.TotalInvalid;
    public int TotalDuplicates => EvaluationSummary.TotalDuplicates;
    public int TotalEligible => EvaluationSummary.TotalEligible;
    public int TotalRejected => EvaluationSummary.TotalRejected;
    public int TotalRebuilt => MutationSummary.TotalRebuilt;
    public int TotalDailyBalancesDeleted => MutationSummary.TotalDailyBalancesDeleted;
    public int TotalProcessedEventsDeleted => MutationSummary.TotalProcessedEventsDeleted;
    public IReadOnlyList<PartialProjectionRebuildItemResult> Items { get; init; }
}
