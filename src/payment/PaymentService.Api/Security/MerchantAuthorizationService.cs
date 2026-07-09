using System.Security.Claims;

using ApiDefaults.Security;

namespace PaymentService.Api.Security;

public sealed class MerchantAuthorizationService : IMerchantAuthorizationService
{
    public const string ClaimType = MerchantClaims.ClaimType;

    public bool IsAuthorized(ClaimsPrincipal user, string merchantId)
        => MerchantClaims.AllowsMerchant(user, merchantId);

    public IReadOnlyCollection<string> GetAuthorizedMerchantIds(ClaimsPrincipal user)
        => MerchantClaims.AuthorizedMerchantIds(user);
}
