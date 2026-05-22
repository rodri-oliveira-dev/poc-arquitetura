using Auth.Api.Security;

namespace Auth.UnitTests.Security;

public sealed class ScopeCatalogTests
{
    [Fact]
    public void ValidScopesAsString_should_join_with_space()
    {
        Assert.Equal(string.Join(' ', ScopeCatalog.ValidScopes), ScopeCatalog.ValidScopesAsString());
    }
}
