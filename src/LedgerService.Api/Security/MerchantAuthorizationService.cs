using System.Security.Claims;

namespace LedgerService.Api.Security;

public sealed class MerchantAuthorizationService : IMerchantAuthorizationService
{
    public const string ClaimType = "merchant_id";

    public bool IsAuthorized(ClaimsPrincipal user, string merchantId)
    {
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(merchantId))
            return false;

        var requestedMerchantId = merchantId.Trim();

        return user.FindAll(ClaimType)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(value => string.Equals(value, requestedMerchantId, StringComparison.Ordinal));
    }
}
