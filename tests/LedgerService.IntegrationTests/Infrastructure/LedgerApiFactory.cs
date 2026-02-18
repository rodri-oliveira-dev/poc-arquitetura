using LedgerService.Infrastructure.Persistence;
using LedgerService.IntegrationTests.Infrastructure.Security;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace LedgerService.IntegrationTests.Infrastructure;

public sealed class LedgerApiFactory : WebApplicationFactory<Program>
{
    private static readonly IServiceProvider InMemoryEfProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

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
            var db = services.SingleOrDefault(d => d.ServiceType == typeof(AppDbContext));
            if (db is not null)
                services.Remove(db);

            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            // LedgerService.Infrastructure registra o provider Npgsql (UseNpgsql). Para usar EF InMemory nos testes,
            // precisamos remover os serviços do provider Npgsql do container; caso contrário o EF detecta
            // múltiplos providers registrados (Npgsql + InMemory) e lança InvalidOperationException.
            var npgsqlProviderDescriptors = services
                .Where(d =>
                    (d.ServiceType.Assembly.GetName().Name?.StartsWith("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.OrdinalIgnoreCase) ?? false)
                    || (d.ImplementationType?.Assembly.GetName().Name?.StartsWith("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.OrdinalIgnoreCase) ?? false)
                    || (d.ImplementationInstance?.GetType().Assembly.GetName().Name?.StartsWith("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();

            foreach (var d in npgsqlProviderDescriptors)
                services.Remove(d);

            // Re-registra o DbContext via InMemory
            services.AddDbContext<AppDbContext>(options =>
            {
                // Nome único por factory para reduzir interferência entre testes.
                options.UseInMemoryDatabase(_dbName);

                // O provider InMemory não suporta transações; o EF emite um warning.
                // Neste projeto warnings são promovidos a exception, então precisamos ignorar.
                options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));

                // Importante: o container da aplicação possui Npgsql + InMemory no grafo de dependências.
                // Para o EF não quebrar com "Only a single database provider can be registered",
                // isolamos os serviços do provider em um service provider interno contendo apenas InMemory.
                options.UseInternalServiceProvider(InMemoryEfProvider);
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
