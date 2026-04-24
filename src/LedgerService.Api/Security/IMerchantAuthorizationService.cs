using System.Security.Claims;

namespace LedgerService.Api.Security;

public interface IMerchantAuthorizationService
{
    bool IsAuthorized(ClaimsPrincipal user, string merchantId);
}
