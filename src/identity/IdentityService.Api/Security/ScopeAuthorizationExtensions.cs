using ApiDefaults.Security;

using Microsoft.AspNetCore.Authorization;

namespace IdentityService.Api.Security;

public static class ScopeAuthorizationExtensions
{
    public static AuthorizationOptions AddScopePolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(ScopePolicies.IdentityReadPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireScope(ScopePolicies.ClaimType, ScopePolicies.IdentityRead);
        });

        options.AddPolicy(ScopePolicies.IdentityWritePolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireScope(ScopePolicies.ClaimType, ScopePolicies.IdentityWrite);
        });

        return options;
    }

}
