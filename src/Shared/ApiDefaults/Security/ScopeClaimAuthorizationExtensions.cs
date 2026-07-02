using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;

namespace ApiDefaults.Security;

public static class ScopeClaimAuthorizationExtensions
{
    public static AuthorizationPolicyBuilder RequireScope(
        this AuthorizationPolicyBuilder policy,
        string claimType,
        string scope)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        return policy.RequireAssertion(context => context.User.HasScope(claimType, scope));
    }

    public static bool HasScope(this ClaimsPrincipal user, string claimType, string scope)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        string? scopeClaim = user.FindFirst(claimType)?.Value;
        return !string.IsNullOrWhiteSpace(scopeClaim)
            && scopeClaim
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(scope, StringComparer.Ordinal);
    }
}
