using ApiDefaults.Security;

using Microsoft.AspNetCore.Authorization;

namespace PaymentService.Api.Security;

public static class ScopeAuthorizationExtensions
{
    public static AuthorizationOptions AddScopePolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(ScopePolicies.PaymentReadPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireScope(ScopePolicies.ClaimType, ScopePolicies.PaymentRead);
        });

        options.AddPolicy(ScopePolicies.PaymentWritePolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireScope(ScopePolicies.ClaimType, ScopePolicies.PaymentWrite);
        });

        return options;
    }
}
