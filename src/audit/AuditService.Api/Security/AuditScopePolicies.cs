using Microsoft.AspNetCore.Authorization;

namespace AuditService.Api.Security;

public static class AuditScopePolicies
{
    public const string ClaimType = "scope";

    public const string AuditWrite = "audit.write";
    public const string AuditRead = "audit.read";
    public const string AuditAdmin = "audit.admin";

    public static AuthorizationOptions AddAuditScopePolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(AuditWrite, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => context.User.HasScope(AuditWrite));
        });

        options.AddPolicy(AuditRead, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => context.User.HasAnyScope(AuditRead, AuditAdmin));
        });

        options.AddPolicy(AuditAdmin, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => context.User.HasScope(AuditAdmin));
        });

        return options;
    }
}
