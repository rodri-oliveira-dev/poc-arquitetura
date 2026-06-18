using Microsoft.AspNetCore.Authorization;

namespace TransferService.Api.Security;

public static class ScopeAuthorizationExtensions
{
    public static AuthorizationOptions AddScopePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(ScopePolicies.TransferReadPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx => HasScope(ctx.User, ScopePolicies.TransferRead));
        });

        options.AddPolicy(ScopePolicies.TransferWritePolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx => HasScope(ctx.User, ScopePolicies.TransferWrite));
        });

        return options;
    }

    private static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string scope)
    {
        var scopeClaim = user.FindFirst(ScopePolicies.ClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(scopeClaim))
            return false;

        return scopeClaim
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(scope, StringComparer.Ordinal);
    }
}
