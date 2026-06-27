namespace IdentityService.Api.Security;

public static class ScopePolicies
{
    public const string ClaimType = "scope";

    public const string IdentityRead = "identity.read";
    public const string IdentityWrite = "identity.write";

    public const string IdentityReadPolicy = "scope:identity.read";
    public const string IdentityWritePolicy = "scope:identity.write";
}
