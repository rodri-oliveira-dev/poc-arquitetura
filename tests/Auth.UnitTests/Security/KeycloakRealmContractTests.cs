using System.Text.Json;

namespace Auth.UnitTests.Security;

public sealed class KeycloakRealmContractTests
{
    [Fact]
    public void Poc_automation_client_should_emit_contract_claims_required_by_business_apis()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRealmPath()));
        var root = document.RootElement;

        var client = FindByName(root.GetProperty("clients"), "clientId", "poc-automation");
        var defaultClientScopes = client.GetProperty("defaultClientScopes")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();

        Assert.Contains("ledger.write", defaultClientScopes);
        Assert.Contains("ledger.read", defaultClientScopes);
        Assert.Contains("balance.read", defaultClientScopes);
        Assert.Contains("transfer.write", defaultClientScopes);
        Assert.Contains("transfer.read", defaultClientScopes);
        Assert.Contains("outbox.admin", defaultClientScopes);
        Assert.Contains("poc-api-audience", defaultClientScopes);
        Assert.Contains("poc-merchants", defaultClientScopes);

        AssertClientScopeIsIncludedInTokenScope(root, "ledger.write");
        AssertClientScopeIsIncludedInTokenScope(root, "ledger.read");
        AssertClientScopeIsIncludedInTokenScope(root, "balance.read");
        AssertClientScopeIsIncludedInTokenScope(root, "transfer.write");
        AssertClientScopeIsIncludedInTokenScope(root, "transfer.read");
        AssertClientScopeIsIncludedInTokenScope(root, "outbox.admin");

        var audienceScope = FindByName(root.GetProperty("clientScopes"), "name", "poc-api-audience");
        var audienceMappers = audienceScope.GetProperty("protocolMappers");
        AssertAudienceMapper(audienceMappers, "ledger-api-audience", "ledger-api");
        AssertAudienceMapper(audienceMappers, "balance-api-audience", "balance-api");
        AssertAudienceMapper(audienceMappers, "transfer-api-audience", "transfer-api");

        var merchantScope = FindByName(root.GetProperty("clientScopes"), "name", "poc-merchants");
        var merchantMapper = FindByName(merchantScope.GetProperty("protocolMappers"), "name", "merchant-id");
        var merchantConfig = merchantMapper.GetProperty("config");
        Assert.Equal("merchant_id", merchantConfig.GetProperty("claim.name").GetString());
        Assert.Equal("true", merchantConfig.GetProperty("access.token.claim").GetString());

        var merchantClaimValue = merchantConfig.GetProperty("claim.value").GetString();
        Assert.Equal("tese m1 m2", merchantClaimValue);
        Assert.DoesNotContain("*", merchantClaimValue);
    }

    [Fact]
    public void Identity_service_admin_client_should_be_able_to_manage_users()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRealmPath()));
        var root = document.RootElement;

        var client = FindByName(root.GetProperty("clients"), "clientId", "identity-service-admin");
        Assert.False(client.GetProperty("publicClient").GetBoolean());
        Assert.True(client.GetProperty("serviceAccountsEnabled").GetBoolean());
        Assert.True(client.GetProperty("fullScopeAllowed").GetBoolean());

        var roleMapper = FindByName(client.GetProperty("protocolMappers"), "name", "realm-management-client-roles");
        var roleMapperConfig = roleMapper.GetProperty("config");
        Assert.Equal("realm-management", roleMapperConfig.GetProperty("usermodel.clientRoleMapping.clientId").GetString());
        Assert.Equal("resource_access.realm-management.roles", roleMapperConfig.GetProperty("claim.name").GetString());
        Assert.Equal("true", roleMapperConfig.GetProperty("access.token.claim").GetString());
    }

    private static void AssertClientScopeIsIncludedInTokenScope(JsonElement root, string scopeName)
    {
        var scope = FindByName(root.GetProperty("clientScopes"), "name", scopeName);
        Assert.Equal("true", scope.GetProperty("attributes").GetProperty("include.in.token.scope").GetString());
    }

    private static void AssertAudienceMapper(JsonElement mappers, string mapperName, string audience)
    {
        var mapper = FindByName(mappers, "name", mapperName);
        var config = mapper.GetProperty("config");
        Assert.Equal(audience, config.GetProperty("included.client.audience").GetString());
        Assert.Equal("true", config.GetProperty("access.token.claim").GetString());
    }

    private static JsonElement FindByName(JsonElement array, string propertyName, string expectedValue)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.TryGetProperty(propertyName, out var property)
                && string.Equals(property.GetString(), expectedValue, StringComparison.Ordinal))
                return item;
        }

        throw new InvalidOperationException($"Item '{expectedValue}' was not found.");
    }

    private static string FindRealmPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "infra", "keycloak", "realm-poc.json");
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find infra/keycloak/realm-poc.json.");
    }
}
