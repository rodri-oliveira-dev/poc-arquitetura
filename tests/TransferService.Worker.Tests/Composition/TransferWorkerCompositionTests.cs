using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using TransferService.Worker.Extensions;
using TransferService.Worker.Ledger;
using TransferService.Worker.Options;

namespace TransferService.Worker.Tests.Composition;

public sealed class TransferWorkerCompositionTests
{
    [Fact]
    public void AddTransferWorkerComposition_should_keep_minimal_composition_root()
    {
        ServiceCollection services = [];
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=transfer-tests;Username=app;Password=app"
        });

        IServiceCollection result = services.AddTransferWorkerComposition(builder.Configuration, builder.Environment);

        Assert.Same(services, result);
    }

    [Fact]
    public void AddTransferWorkerComposition_should_register_ledger_client_credentials_authentication()
    {
        ServiceCollection services = [];
        HostApplicationBuilder builder = CreateBuilderWithRequiredWorkerConfiguration(clientSecret: "local-secret");

        services.AddTransferWorkerComposition(builder.Configuration, builder.Environment);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        Assert.IsType<ClientCredentialsLedgerAccessTokenProvider>(provider.GetRequiredService<ILedgerAccessTokenProvider>());
        Assert.NotNull(provider.GetRequiredService<LedgerAuthenticationHandler>());
    }

    [Fact]
    public void TransferWorkerOptions_should_fail_early_when_ledger_client_secret_is_missing()
    {
        ServiceCollection services = [];
        HostApplicationBuilder builder = CreateBuilderWithRequiredWorkerConfiguration(clientSecret: "");

        services.AddTransferWorkerComposition(builder.Configuration, builder.Environment);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<TransferWorkerOptions>>().Value);

        Assert.Contains(exception.Failures, failure => failure.Contains("ClientSecret", StringComparison.Ordinal));
    }

    private static HostApplicationBuilder CreateBuilderWithRequiredWorkerConfiguration(string clientSecret)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=transfer-tests;Username=app;Password=app",
            ["TransferService:Worker:Kafka:BootstrapServers"] = "localhost:9092",
            ["TransferService:Worker:Ledger:BaseAddress"] = "http://ledger-service:8080",
            ["TransferService:Worker:Ledger:Auth:TokenEndpoint"] = "http://keycloak:8080/realms/poc/protocol/openid-connect/token",
            ["TransferService:Worker:Ledger:Auth:ClientId"] = "poc-automation",
            ["TransferService:Worker:Ledger:Auth:ClientSecret"] = clientSecret,
            ["TransferService:Worker:Ledger:Auth:Scope"] = "ledger.write"
        });

        return builder;
    }
}
