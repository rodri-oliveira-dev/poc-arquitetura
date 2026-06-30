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
            policy.RequireAssertion(ctx => HasScope(ctx.User, ScopePolicies.IdentityRead));
        });

        options.AddPolicy(ScopePolicies.IdentityWritePolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx => HasScope(ctx.User, ScopePolicies.IdentityWrite));
        });

        return options;
    }

    private static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string scope)
    {
        var scopeClaim = user.FindFirst(ScopePolicies.ClaimType)?.Value;
        return !string.IsNullOrWhiteSpace(scopeClaim) && scopeClaim
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(scope, StringComparer.Ordinal);
    }
}
