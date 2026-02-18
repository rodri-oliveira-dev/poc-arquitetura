using Microsoft.AspNetCore.Authorization;

namespace LedgerService.Api.Security;

public static class ScopeAuthorizationExtensions
{
    /// <summary>
    /// Registra policies baseadas na claim "scope" (string com scopes separados por espaço).
    /// </summary>
    public static AuthorizationOptions AddScopePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(ScopePolicies.LedgerReadPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx => HasScope(ctx.User, ScopePolicies.LedgerRead));
        });

        options.AddPolicy(ScopePolicies.LedgerWritePolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx => HasScope(ctx.User, ScopePolicies.LedgerWrite));
        });

        return options;
    }

    private static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string scope)
    {
        var scopeClaim = user.FindFirst(ScopePolicies.ClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(scopeClaim))
            return false;

        // Requisito: scopes separados por espaço.
        // Comparação estrita por token (evita "ledger.write" bater em "ledger.writeX").
        return scopeClaim
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(scope, StringComparer.Ordinal);
    }
}