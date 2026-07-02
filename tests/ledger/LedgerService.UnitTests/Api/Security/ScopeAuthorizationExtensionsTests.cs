using System.Security.Claims;

using ApiDefaults.Security;

using LedgerService.Api.Security;

namespace LedgerService.UnitTests.Api.Security;

public sealed class ScopeAuthorizationExtensionsTests
{
    private static ClaimsPrincipal PrincipalWithScope(string? scope)
    {
        var claims = new List<Claim>();
        if (scope is not null)
            claims.Add(new Claim(ScopePolicies.ClaimType, scope));

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void HasScope_should_return_false_when_scope_claim_missing()
    {
        var principal = PrincipalWithScope(null);
        bool result = principal.HasScope(ScopePolicies.ClaimType, ScopePolicies.LedgerWrite);
        Assert.False(result);
    }

    [Fact]
    public void HasScope_should_match_exact_token_only()
    {
        var principal = PrincipalWithScope("ledger.writeX ledger.write");
        bool result = principal.HasScope(ScopePolicies.ClaimType, ScopePolicies.LedgerWrite);
        Assert.True(result);
    }
}
