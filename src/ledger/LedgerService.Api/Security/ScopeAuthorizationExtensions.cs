using ApiDefaults.Security;

using Microsoft.AspNetCore.Authorization;

namespace LedgerService.Api.Security;

public static class ScopeAuthorizationExtensions
{
    /// <summary>
    /// Registra policies baseadas na claim "scope" (string com scopes separados por espaço).
    /// </summary>
    public static AuthorizationOptions AddScopePolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(ScopePolicies.LedgerReadPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireScope(ScopePolicies.ClaimType, ScopePolicies.LedgerRead);
        });

        options.AddPolicy(ScopePolicies.LedgerWritePolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireScope(ScopePolicies.ClaimType, ScopePolicies.LedgerWrite);
        });

        options.AddPolicy(ScopePolicies.OutboxAdminPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireScope(ScopePolicies.ClaimType, ScopePolicies.OutboxAdmin);
        });

        return options;
    }

}
