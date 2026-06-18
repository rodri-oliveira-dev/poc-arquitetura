using System.Security.Claims;

namespace ApiDefaults.Security;

public static class MerchantClaims
{
    public const string ClaimType = "merchant_id";

    public static bool AllowsMerchant(ClaimsPrincipal user, string merchantId)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(merchantId);

        if (user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(merchantId))
        {
            return false;
        }

        string requestedMerchantId = merchantId.Trim();
        return AuthorizedMerchantIds(user)
            .Any(value => string.Equals(value, requestedMerchantId, StringComparison.Ordinal));
    }

    public static IReadOnlyCollection<string> AuthorizedMerchantIds(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.Identity?.IsAuthenticated != true
            ? []
            : user.FindAll(ClaimType)
            .SelectMany(static claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
