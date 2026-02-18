using BalanceService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using BalanceService.IntegrationTests.Infrastructure.Security;

namespace BalanceService.IntegrationTests.Infrastructure;

public sealed class BalanceApiFactory : WebApplicationFactory<Program>
{
    private static readonly IServiceProvider InMemoryEfProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    private readonly string _dbName = $"balance-it-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:Enabled"] = "false",
                ["Jwt:Issuer"] = "https://auth-api",
                ["Jwt:Audience"] = "balance-api",
                ["Jwt:JwksUrl"] = "http://localhost/jwks.json",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=ignore;Username=ignore;Password=ignore"
            });
        });

        builder.ConfigureServices(services =>
        {
            var db = services.SingleOrDefault(d => d.ServiceType == typeof(BalanceDbContext));
            if (db is not null)
                services.Remove(db);

            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<BalanceDbContext>));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            // BalanceService.Infrastructure registra o provider Npgsql (UseNpgsql). Para usar EF InMemory nos testes,
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

            // Nome único por factory para reduzir interferência entre testes.
            services.AddDbContext<BalanceDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);

                // Importante: o container da aplicação possui Npgsql + InMemory no grafo de dependências.
                // Para o EF não quebrar com "Only a single database provider can be registered",
                // isolamos os serviços do provider em um service provider interno contendo apenas InMemory.
                options.UseInternalServiceProvider(InMemoryEfProvider);
            });

            var hostedServices = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
            foreach (var d in hostedServices)
                services.Remove(d);

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
