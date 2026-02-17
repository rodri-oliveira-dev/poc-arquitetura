using BalanceService.Api.Security;
using FluentAssertions;
using System.Security.Claims;

namespace BalanceService.UnitTests.Tests;

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
        var method = typeof(ScopeAuthorizationExtensions)
            .GetMethod("HasScope", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (bool)method!.Invoke(null, [principal, ScopePolicies.BalanceRead])!;
        result.Should().BeFalse();
    }

    [Fact]
    public void HasScope_should_find_scope_in_space_separated_list()
    {
        var principal = PrincipalWithScope("balance.read ledger.write");
        var method = typeof(ScopeAuthorizationExtensions)
            .GetMethod("HasScope", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (bool)method!.Invoke(null, [principal, ScopePolicies.BalanceRead])!;
        result.Should().BeTrue();
    }
}
