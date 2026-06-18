using System.Security.Claims;

using ApiDefaults.Security;

namespace BalanceService.Api.Security;

public sealed class MerchantAuthorizationService : IMerchantAuthorizationService
{
    public const string ClaimType = MerchantClaims.ClaimType;

    public bool IsAuthorized(ClaimsPrincipal user, string merchantId)
        => MerchantClaims.AllowsMerchant(user, merchantId);
}
