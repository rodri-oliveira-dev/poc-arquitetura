using System.Security.Claims;

using IdentityService.Api.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.UnitTests.Api.Security;

public sealed class ScopeAuthorizationExtensionsTests
{
    [Theory]
    [InlineData(ScopePolicies.IdentityWritePolicy, "identity.write", true)]
    [InlineData(ScopePolicies.IdentityWritePolicy, "identity.read", false)]
    [InlineData(ScopePolicies.IdentityReadPolicy, "identity.read identity.write", true)]
    [InlineData(ScopePolicies.IdentityReadPolicy, "", false)]
    public async Task Scope_policies_should_authorize_only_when_required_scope_is_present_Async(
        string policyName,
        string scopes,
        bool expected)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddScopePolicies());

        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(CreateUser(scopes), resource: null, policyName);

        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public void AddScopePolicies_should_validate_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => ScopeAuthorizationExtensions.AddScopePolicies(null!));
    }

    private static ClaimsPrincipal CreateUser(string scopes)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, "identity-user")
        ];

        if (scopes.Length > 0)
            claims.Add(new Claim(ScopePolicies.ClaimType, scopes));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }
}
