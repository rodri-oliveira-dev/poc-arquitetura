using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Auth.IntegrationTests.Infrastructure;

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly IReadOnlyDictionary<string, string?> _configurationOverrides;

    public AuthApiFactory()
        : this(new Dictionary<string, string?>())
    {
    }

    private AuthApiFactory(IReadOnlyDictionary<string, string?> configurationOverrides)
    {
        _configurationOverrides = configurationOverrides;
    }

    public static AuthApiFactory WithConfigurationOverrides(IReadOnlyDictionary<string, string?> configurationOverrides)
        => new(configurationOverrides);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddDebug();
        });

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            // Isola escrita de chave RSA em arquivo para um path temporário.
            // Não colocar segredo em repo.
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Issuer"] = "https://auth-api",
                ["Auth:TokenLifetimeMinutes"] = "10",
                ["Auth:KeyPath"] = ".\\TestResults\\auth-test-key.json",
                ["Auth:DevelopmentUser:Username"] = "poc-usuario",
                ["Auth:DevelopmentUser:Password"] = "Poc#123",
                ["Auth:LoginRateLimit:PermitLimit"] = "100",
                ["Auth:LoginRateLimit:WindowSeconds"] = "60",
                ["Swagger:Enabled"] = "false"
            });

            cfg.AddInMemoryCollection(_configurationOverrides);
        });
    }
}
