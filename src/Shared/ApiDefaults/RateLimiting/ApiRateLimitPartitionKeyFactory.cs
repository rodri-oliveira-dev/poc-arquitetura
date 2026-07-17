using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using ApiDefaults.Security;

namespace ApiDefaults.RateLimiting;

internal static class ApiRateLimitPartitionKeyFactory
{
    private const string MissingIpPartition = "ip:unknown";

    public static string CreateAuthenticatedKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ClaimsPrincipal user = context.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return CreateAnonymousIpKey(context);
        }

        string? clientOrSubject = FirstClaimValue(
            user,
            "client_id",
            "azp",
            "sub",
            ClaimTypes.NameIdentifier);

        string actor = string.IsNullOrWhiteSpace(clientOrSubject)
            ? $"missing-client:{CreateAnonymousIpKey(context)}"
            : $"client:{Normalize(clientOrSubject)}";

        string merchant = CreateMerchantComponent(user);

        return HashPartitionKey($"auth|{actor}|{merchant}");
    }

    public static string CreateAnonymousIpKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string? remoteIp = context.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(remoteIp)
            ? MissingIpPartition
            : HashPartitionKey($"ip|{remoteIp}");
    }

    public static string DescribeAuthenticatedPartitionType(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User.Identity?.IsAuthenticated != true)
        {
            return "anonymous_ip";
        }

        return FirstClaimValue(
            context.User,
            "client_id",
            "azp",
            "sub",
            ClaimTypes.NameIdentifier) is null
            ? "authenticated_ip_fallback"
            : "authenticated_claims";
    }

    private static string CreateMerchantComponent(ClaimsPrincipal user)
    {
        string[] merchantIds = MerchantClaims.AuthorizedMerchantIds(user)
            .Select(Normalize)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return merchantIds.Length == 0
            ? "merchant:none"
            : $"merchant:{string.Join(",", merchantIds)}";
    }

    private static string? FirstClaimValue(ClaimsPrincipal user, params string[] claimTypes)
        => claimTypes
            .Select(type => user.FindFirst(type)?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string Normalize(string value)
        => value.Trim();

    private static string HashPartitionKey(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
