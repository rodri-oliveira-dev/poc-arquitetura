using ApiDefaults.Options;

namespace BalanceService.Api.Options;

public sealed class ApiLimitsOptions : ApiDefaultsOptions
{
    public int MaxBalancePeriodDays { get; init; } = 31;
}
