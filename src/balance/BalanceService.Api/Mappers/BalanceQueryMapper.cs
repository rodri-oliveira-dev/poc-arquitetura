using System.Globalization;

using BalanceService.Application.Balances.Queries;

using FluentValidation;
using FluentValidation.Results;

namespace BalanceService.Api.Mappers;

public static class BalanceQueryMapper
{
    public static GetDailyBalanceQuery ToDailyQuery(string merchantId, string date)
        => new(merchantId, ParseDateOrThrow(date, nameof(date)));

    public static GetPeriodBalanceQuery ToPeriodQuery(string merchantId, string from, string to, int maxPeriodDays)
    {
        var parsedFrom = ParseDateOrThrow(from, nameof(from));
        var parsedTo = ParseDateOrThrow(to, nameof(to));

        ValidateMaxPeriod(parsedFrom, parsedTo, maxPeriodDays);

        return new(merchantId, parsedFrom, parsedTo);
    }

    private static DateOnly ParseDateOrThrow(string rawValue, string parameterName)
    {
        if (DateOnly.TryParseExact(rawValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            return parsedDate;

        throw new ValidationException(new[]
        {
            new ValidationFailure(parameterName, $"{parameterName} must be in format YYYY-MM-DD.")
        });
    }

    private static void ValidateMaxPeriod(DateOnly from, DateOnly to, int maxPeriodDays)
    {
        if (from > to)
            return;

        var periodDays = (to.DayNumber - from.DayNumber) + 1;
        if (periodDays <= maxPeriodDays)
            return;

        throw new ValidationException(new[]
        {
            new ValidationFailure(nameof(to), $"Period must be at most {maxPeriodDays} days.")
        });
    }
}
