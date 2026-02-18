using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using LedgerService.Infrastructure.Persistence;
using LedgerService.IntegrationTests.Infrastructure.Security;

namespace LedgerService.IntegrationTests.Infrastructure;

public sealed class LedgerApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"ledger-it-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // desliga kafka/outbox na infra durante testes
                ["Kafka:Enabled"] = "false",

                // Jwt: precisa existir para o AddApiJwtAuth não lançar.
                // Nos testes de integração, vamos injetar um token válido via TestAuthHandler,
                // então os valores aqui são apenas placeholders.
                ["Jwt:Issuer"] = "https://auth-api",
                ["Jwt:Audience"] = "ledger-api",
                ["Jwt:JwksUrl"] = "http://localhost/jwks.json",

                // DB placeholder (não será usada neste conjunto mínimo de integração)
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=ignore;Username=ignore;Password=ignore"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove AppDbContext (Npgsql) e substitui por EF InMemory para integração leve.
            // Isso permite exercitar o pipeline HTTP real sem depender de Postgres.
            var appDbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (appDbContextDescriptor is not null)
                services.Remove(appDbContextDescriptor);

            // Re-registra o DbContext via InMemory
            services.AddDbContext<AppDbContext>(options =>
            {
                // Nome único por factory para reduzir interferência entre testes.
                options.UseInMemoryDatabase(_dbName);
            });

            // Também remove hosted services se algum ainda tiver sido registrado.
            // (por segurança, já que Kafka:Enabled=false deveria impedir)
            var hostedServices = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
            foreach (var d in hostedServices)
                services.Remove(d);

            // Usa validação JWT real (JwtBearer), mas com chave RSA estática para testes
            // (sem chamada remota a JWKS, que não funciona com TestServer em memória).
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.ConfigurationManager = null;
                options.TokenValidationParameters.IssuerSigningKey = new RsaSecurityKey(TestJwtKeys.Rsa)
                {
                    KeyId = TestJwtKeys.Kid
                };
            });
        });
    }
}
