using System.Security.Claims;

namespace BalanceService.Api.Security;

public interface IMerchantAuthorizationService
{
    bool IsAuthorized(ClaimsPrincipal user, string merchantId);
}
