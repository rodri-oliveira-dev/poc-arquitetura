using System.Security.Claims;

using ApiDefaults.Security;

using BalanceService.Api.Security;

namespace BalanceService.UnitTests.Api.Security;

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
    public void HasScope_should_return_false_when_empty()
    {
        var principal = PrincipalWithScope(" ");
        bool result = principal.HasScope(ScopePolicies.ClaimType, ScopePolicies.BalanceRead);
        Assert.False(result);
    }

    [Fact]
    public void HasScope_should_find_scope_in_space_separated_list()
    {
        var principal = PrincipalWithScope("balance.read ledger.write");
        bool result = principal.HasScope(ScopePolicies.ClaimType, ScopePolicies.BalanceRead);
        Assert.True(result);
    }
}
