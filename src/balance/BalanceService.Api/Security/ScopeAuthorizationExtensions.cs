using ApiDefaults.Security;

using Microsoft.AspNetCore.Authorization;

namespace BalanceService.Api.Security;

public static class ScopeAuthorizationExtensions
{
    /// <summary>
    /// Registra policies baseadas na claim "scope" (string com scopes separados por espaço).
    /// </summary>
    public static AuthorizationOptions AddScopePolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(ScopePolicies.BalanceReadPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireScope(ScopePolicies.ClaimType, ScopePolicies.BalanceRead);
        });

        options.AddPolicy(ScopePolicies.BalanceWritePolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireScope(ScopePolicies.ClaimType, ScopePolicies.BalanceWrite);
        });

        return options;
    }

}
