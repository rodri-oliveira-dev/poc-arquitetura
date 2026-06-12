using System.Text.Json.Serialization;

namespace BalanceService.Application.Balances.Replay;

public sealed record ProjectionRebuildDivergenceItem
{
    public ProjectionRebuildDivergenceItem(
        ProjectionRebuildDivergenceIdentity identity,
        ProjectionRebuildDivergenceValues values,
        ProjectionRebuildDivergenceCounters counters)
    {
        Identity = identity;
        Values = values;
        Counters = counters;
    }

    [JsonIgnore]
    public ProjectionRebuildDivergenceIdentity Identity { get; init; }

    [JsonIgnore]
    public ProjectionRebuildDivergenceValues Values { get; init; }

    [JsonIgnore]
    public ProjectionRebuildDivergenceCounters Counters { get; init; }

    public string? AccountId => Identity.AccountId;
    public string MerchantId => Identity.MerchantId;
    public DateOnly Date => Identity.Date;
    public string Currency => Identity.Currency;
    public decimal CurrentBalance => Values.CurrentBalance;
    public decimal RebuiltBalance => Values.RebuiltBalance;
    public decimal Difference => Values.Difference;
    public int EventsAnalyzed => Counters.EventsAnalyzed;
    public int InvalidEvents => Counters.InvalidEvents;
    public int DuplicateEventsIgnored => Counters.DuplicateEventsIgnored;
}
