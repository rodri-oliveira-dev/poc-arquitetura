using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using AuditService.Application.FunctionalAuditing.CreateAuditRecord;

namespace AuditService.Api.Security;

public static class AuditClaimsPrincipalExtensions
{
    public static bool HasScope(this ClaimsPrincipal user, string scope)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        return user.FindAll(AuditScopePolicies.ClaimType)
            .SelectMany(static claim => SplitClaimValues(claim.Value))
            .Contains(scope, StringComparer.Ordinal);
    }

    public static bool HasAnyScope(this ClaimsPrincipal user, params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(scopes);

        return scopes.Any(user.HasScope);
    }

    public static bool IsAuditAdmin(this ClaimsPrincipal user)
        => user.HasScope(AuditScopePolicies.AuditAdmin);

    public static IReadOnlyCollection<string> MerchantIds(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return [.. user.FindAll("merchant_id")
            .SelectMany(static claim => SplitClaimValues(claim.Value))
            .Distinct(StringComparer.Ordinal)];
    }

    public static bool CanAccessMerchant(this ClaimsPrincipal user, string? merchantId)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.IsAuditAdmin()
            || (!string.IsNullOrWhiteSpace(merchantId)
                && user.MerchantIds().Contains(merchantId, StringComparer.Ordinal));
    }

    public static CreateAuditRecordActor? ResolveActor(this ClaimsPrincipal user, CreateAuditRecordActor? bodyActor)
    {
        ArgumentNullException.ThrowIfNull(user);

        string? subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        string? clientId = user.FindFirstValue("client_id")
            ?? user.FindFirstValue("azp");

        return !string.IsNullOrWhiteSpace(subject) || !string.IsNullOrWhiteSpace(clientId)
            ? new CreateAuditRecordActor(
                string.IsNullOrWhiteSpace(subject) ? "Client" : "User",
                subject,
                clientId)
            : bodyActor;
    }

    private static string[] SplitClaimValues(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
