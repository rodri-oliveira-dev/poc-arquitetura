using FluentAssertions;
using LedgerService.Api.Security;
using System.Security.Claims;

namespace LedgerService.UnitTests.Tests;

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
        var method = typeof(ScopeAuthorizationExtensions)
            .GetMethod("HasScope", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.Should().NotBeNull();

        var result = (bool)method!.Invoke(null, [principal, ScopePolicies.LedgerWrite])!;
        result.Should().BeFalse();
    }

    [Fact]
    public void HasScope_should_match_exact_token_only()
    {
        var principal = PrincipalWithScope("ledger.writeX ledger.write");
        var method = typeof(ScopeAuthorizationExtensions)
            .GetMethod("HasScope", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (bool)method!.Invoke(null, [principal, ScopePolicies.LedgerWrite])!;
        result.Should().BeTrue();
    }
}
