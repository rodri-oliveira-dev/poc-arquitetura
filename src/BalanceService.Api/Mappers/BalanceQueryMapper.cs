using System.Globalization;

using BalanceService.Application.Balances.Queries;

using FluentValidation;
using FluentValidation.Results;

namespace BalanceService.Api.Mappers;

public static class BalanceQueryMapper
{
    public static GetDailyBalanceQuery ToDailyQuery(string merchantId, string date)
        => new(merchantId, ParseDateOrThrow(date, nameof(date)));

    public static GetPeriodBalanceQuery ToPeriodQuery(string merchantId, string from, string to)
        => new(merchantId, ParseDateOrThrow(from, nameof(from)), ParseDateOrThrow(to, nameof(to)));

    private static DateOnly ParseDateOrThrow(string rawValue, string parameterName)
    {
        if (DateOnly.TryParseExact(rawValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            return parsedDate;

        throw new ValidationException(new[]
        {
            new ValidationFailure(parameterName, $"{parameterName} must be in format YYYY-MM-DD.")
        });
    }
}
