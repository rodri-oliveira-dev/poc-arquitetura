using System.Security.Claims;

using ApiDefaults.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

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
    public void HasScope_should_return_false_for_unauthenticated_user()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ScopeClaimType, ReadScope)]));

        bool result = principal.HasScope(ScopeClaimType, ReadScope);

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
    public void HasScope_should_find_scope_in_multiple_claims()
    {
        var principal = PrincipalWithClaims(
        [
            new Claim(ScopeClaimType, ReadScope),
            new Claim(ScopeClaimType, WriteScope)
        ]);

        bool result = principal.HasScope(ScopeClaimType, WriteScope);

        Assert.True(result);
    }

    [Fact]
    public void HasScope_should_allow_duplicate_scope()
    {
        var principal = PrincipalWithScope($"{WriteScope} {WriteScope}");

        bool result = principal.HasScope(ScopeClaimType, WriteScope);

        Assert.True(result);
    }

    [Theory]
    [InlineData("payments.read.all")]
    [InlineData("xpayments.read")]
    [InlineData("Payments.Read")]
    public void HasScope_should_match_exact_case_sensitive_token_only(string claimValue)
    {
        var principal = PrincipalWithScope(claimValue);

        bool result = principal.HasScope(ScopeClaimType, "payments.read");

        Assert.False(result);
    }

    [Fact]
    public void HasScope_should_not_match_partial_token_when_exact_token_is_absent()
    {
        var principal = PrincipalWithScope($"{WriteScope}X");

        bool result = principal.HasScope(ScopeClaimType, WriteScope);

        Assert.False(result);
    }

    [Fact]
    public async Task RequireScope_should_register_requirement_and_authorize_matching_user_Async()
    {
        AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
            .RequireScope(ScopeClaimType, ReadScope)
            .Build();
        var authorizationService = CreateAuthorizationService(policy);
        var principal = PrincipalWithScope(ReadScope);

        AuthorizationResult result = await authorizationService.AuthorizeAsync(principal, resource: null, "scope-policy");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task RequireScope_should_deny_missing_scope_Async()
    {
        AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
            .RequireScope(ScopeClaimType, ReadScope)
            .Build();
        var authorizationService = CreateAuthorizationService(policy);
        var principal = PrincipalWithScope(WriteScope);

        AuthorizationResult result = await authorizationService.AuthorizeAsync(principal, resource: null, "scope-policy");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void RequireScope_should_throw_for_invalid_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => ScopeClaimAuthorizationExtensions.RequireScope(null!, ScopeClaimType, ReadScope));
        Assert.Throws<ArgumentException>(() => new AuthorizationPolicyBuilder().RequireScope("", ReadScope));
        Assert.Throws<ArgumentException>(() => new AuthorizationPolicyBuilder().RequireScope(ScopeClaimType, ""));
    }

    [Fact]
    public void HasScope_should_throw_for_invalid_arguments()
    {
        var principal = PrincipalWithScope(ReadScope);

        Assert.Throws<ArgumentNullException>(() => ScopeClaimAuthorizationExtensions.HasScope(null!, ScopeClaimType, ReadScope));
        Assert.Throws<ArgumentException>(() => principal.HasScope("", ReadScope));
        Assert.Throws<ArgumentException>(() => principal.HasScope(ScopeClaimType, ""));
    }

    private static ClaimsPrincipal PrincipalWithScope(string? scope)
    {
        var claims = new List<Claim>();
        if (scope is not null)
            claims.Add(new Claim(ScopeClaimType, scope));

        return PrincipalWithClaims(claims);
    }

    private static ClaimsPrincipal PrincipalWithClaims(IEnumerable<Claim> claims)
        => new(new ClaimsIdentity(claims, authenticationType: "jwt"));

    private static IAuthorizationService CreateAuthorizationService(AuthorizationPolicy policy)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddAuthorization(options => options.AddPolicy("scope-policy", policy));
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }
}
