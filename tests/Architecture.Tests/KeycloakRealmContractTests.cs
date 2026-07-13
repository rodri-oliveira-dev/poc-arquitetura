using System.Text.Json;

namespace Architecture.Tests;

public sealed class KeycloakRealmContractTests
{
    [Fact]
    public void Local_realm_should_issue_payment_audience_scopes_and_merchant_claim()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(GetRepositoryPath("infra/keycloak/realm-poc.json")));
        var root = document.RootElement;

        var clients = root.GetProperty("clients").EnumerateArray().ToArray();
        var clientScopes = root.GetProperty("clientScopes").EnumerateArray().ToArray();
        var users = root.GetProperty("users").EnumerateArray().ToArray();

        var paymentAudienceClient = clients.Single(client => GetString(client, "clientId") == "payment-api");
        Assert.Equal("PaymentService.Api audience", GetString(paymentAudienceClient, "name"));
        Assert.False(GetBool(paymentAudienceClient, "standardFlowEnabled"));
        Assert.False(GetBool(paymentAudienceClient, "implicitFlowEnabled"));
        Assert.False(GetBool(paymentAudienceClient, "directAccessGrantsEnabled"));
        Assert.False(GetBool(paymentAudienceClient, "serviceAccountsEnabled"));

        var audienceScope = clientScopes.Single(scope => GetString(scope, "name") == "poc-api-audience");
        var paymentAudienceMapper = audienceScope.GetProperty("protocolMappers")
            .EnumerateArray()
            .Single(mapper => GetString(mapper, "name") == "payment-api-audience");
        Assert.Equal("payment-api", GetString(paymentAudienceMapper.GetProperty("config"), "included.client.audience"));
        Assert.Equal("true", GetString(paymentAudienceMapper.GetProperty("config"), "access.token.claim"));

        AssertPaymentScope(clientScopes, "payment.write", "Permite criar pagamentos.");
        AssertPaymentScope(clientScopes, "payment.read", "Permite consultar pagamentos.");
        AssertPaymentScope(clientScopes, "payment.refund", "Permite solicitar refunds.");

        AssertClientHasPaymentAccess(clients, "poc-automation");
        AssertClientHasPaymentAccess(clients, "poc-local-admin-debug");

        var merchantScope = clientScopes.Single(scope => GetString(scope, "name") == "poc-merchants");
        var merchantMapper = merchantScope.GetProperty("protocolMappers")
            .EnumerateArray()
            .Single(mapper => GetString(mapper, "name") == "merchant-id");
        Assert.Equal("merchant_id", GetString(merchantMapper.GetProperty("config"), "claim.name"));

        var adminUser = users.Single(user => GetString(user, "username") == "local_admin_user");
        var expectedScopes = adminUser.GetProperty("attributes").GetProperty("expected_scopes")[0].GetString();
        Assert.Contains("payment.write payment.read payment.refund", expectedScopes, StringComparison.Ordinal);
    }

    private static void AssertPaymentScope(JsonElement[] clientScopes, string name, string description)
    {
        var scope = clientScopes.Single(scope => GetString(scope, "name") == name);
        Assert.Equal(description, GetString(scope, "description"));
        Assert.Equal("true", GetString(scope.GetProperty("attributes"), "include.in.token.scope"));
    }

    private static void AssertClientHasPaymentAccess(JsonElement[] clients, string clientId)
    {
        var client = clients.Single(client => GetString(client, "clientId") == clientId);
        Assert.False(GetBool(client, "fullScopeAllowed"));

        var defaultScopes = client.GetProperty("defaultClientScopes")
            .EnumerateArray()
            .Select(scope => scope.GetString())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("poc-api-audience", defaultScopes);
        Assert.Contains("poc-merchants", defaultScopes);
        Assert.Contains("payment.write", defaultScopes);
        Assert.Contains("payment.read", defaultScopes);
        Assert.Contains("payment.refund", defaultScopes);
    }

    private static string GetRepositoryPath(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PocArquitetura.slnx")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("Repository root not found.");

        return Path.Combine(directory.FullName, relativePath);
    }

    private static string? GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) ? property.GetString() : null;

    private static bool GetBool(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.GetBoolean();
}
