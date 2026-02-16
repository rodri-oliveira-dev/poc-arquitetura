using Microsoft.AspNetCore.Authorization;

namespace BalanceService.Api.Security;

public static class ScopeAuthorizationExtensions
{
    /// <summary>
    /// Registra policies baseadas na claim "scope" (string com scopes separados por espaço).
    /// </summary>
    public static AuthorizationOptions AddScopePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(ScopePolicies.BalanceReadPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx => HasScope(ctx.User, ScopePolicies.BalanceRead));
        });

        options.AddPolicy(ScopePolicies.BalanceWritePolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx => HasScope(ctx.User, ScopePolicies.BalanceWrite));
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