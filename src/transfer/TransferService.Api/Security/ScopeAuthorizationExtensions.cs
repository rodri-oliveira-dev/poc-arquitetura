using ApiDefaults.Security;

using Microsoft.AspNetCore.Authorization;

namespace TransferService.Api.Security;

public static class ScopeAuthorizationExtensions
{
    public static AuthorizationOptions AddScopePolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(ScopePolicies.TransferReadPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireScope(ScopePolicies.ClaimType, ScopePolicies.TransferRead);
        });

        options.AddPolicy(ScopePolicies.TransferWritePolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireScope(ScopePolicies.ClaimType, ScopePolicies.TransferWrite);
        });

        return options;
    }

}
