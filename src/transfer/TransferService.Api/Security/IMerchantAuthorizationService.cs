using System.Security.Claims;

namespace TransferService.Api.Security;

public interface IMerchantAuthorizationService
{
    bool IsAuthorized(ClaimsPrincipal user, string merchantId);

    IReadOnlyCollection<string> GetAuthorizedMerchantIds(ClaimsPrincipal user);
}
