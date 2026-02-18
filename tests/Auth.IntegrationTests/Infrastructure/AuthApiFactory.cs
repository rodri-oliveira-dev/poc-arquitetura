using Auth.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Auth.IntegrationTests.Infrastructure;

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            // Isola escrita de chave RSA em arquivo para um path temporário.
            // Não colocar segredo em repo.
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Issuer"] = "https://auth-api",
                ["Auth:TokenLifetimeMinutes"] = "10",
                ["Auth:KeyPath"] = ".\\TestResults\\auth-test-key.json",
                ["Swagger:Enabled"] = "false"
            });
        });
    }
}
