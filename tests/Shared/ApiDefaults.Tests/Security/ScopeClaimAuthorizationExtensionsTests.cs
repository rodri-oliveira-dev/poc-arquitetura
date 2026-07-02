using System.Security.Claims;

using ApiDefaults.Security;

namespace ApiDefaults.Tests.Security;

public sealed class ScopeClaimAuthorizationExtensionsTests
{
    private const string ScopeClaimType = "scope";
    private const string ReadScope = "shared.read";
    private const string WriteScope = "shared.write";

    [Fact]
    public void HasScope_should_return_false_when_scope_claim_missing()
    {
        var principal = PrincipalWithScope(null);

        bool result = principal.HasScope(ScopeClaimType, WriteScope);

        Assert.False(result);
    }

    [Fact]
    public void HasScope_should_return_false_when_empty()
    {
        var principal = PrincipalWithScope(" ");

        bool result = principal.HasScope(ScopeClaimType, ReadScope);

        Assert.False(result);
    }

    [Fact]
    public void HasScope_should_find_scope_in_space_separated_list()
    {
        var principal = PrincipalWithScope($"{ReadScope} {WriteScope}");

        bool result = principal.HasScope(ScopeClaimType, WriteScope);

        Assert.True(result);
    }

    [Fact]
    public void HasScope_should_match_exact_token_only()
    {
        var principal = PrincipalWithScope($"{WriteScope}X {WriteScope}");

        bool result = principal.HasScope(ScopeClaimType, WriteScope);

        Assert.True(result);
    }

    private static ClaimsPrincipal PrincipalWithScope(string? scope)
    {
        var claims = new List<Claim>();
        if (scope is not null)
            claims.Add(new Claim(ScopeClaimType, scope));

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        return new ClaimsPrincipal(identity);
    }
}
