using Auth.Api.Security;
using FluentAssertions;

namespace Auth.UnitTests.Tests;

public sealed class ScopeCatalogTests
{
    [Fact]
    public void ValidScopesAsString_should_join_with_space()
    {
        ScopeCatalog.ValidScopesAsString().Should().Be(string.Join(' ', ScopeCatalog.ValidScopes));
    }
}
