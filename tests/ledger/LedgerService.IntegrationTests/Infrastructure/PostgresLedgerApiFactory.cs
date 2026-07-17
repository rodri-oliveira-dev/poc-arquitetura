using LedgerService.Infrastructure.Persistence;
using LedgerService.IntegrationTests.Infrastructure.Security;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace LedgerService.IntegrationTests.Infrastructure;

public sealed class PostgresLedgerApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public PostgresLedgerApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Test");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddDebug();
        });

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:Enabled"] = "false",
                ["Estornos:Processor:Enabled"] = "false",
                ["Jwt:Issuer"] = TestJwtTokenFactory.KeycloakIssuer,
                ["Jwt:Audience"] = "ledger-api",
                ["Jwt:JwksUrl"] = "https://localhost/jwks.json",
                ["ApiLimits:MaxRequestBodySizeBytes"] = "128",
                ["ForwardedHeaders:TrustedProxies:0"] = "127.0.0.1",
                ["ForwardedHeaders:AllowedHosts:0"] = "localhost",
                ["ConnectionStrings:DefaultConnection"] = _connectionString
            });
        });

        builder.ConfigureServices(services =>
        {
            var db = services.SingleOrDefault(d => d.ServiceType == typeof(AppDbContext));
            if (db is not null)
                services.Remove(db);

            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(
                    _connectionString,
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "ledger")));

            var hostedServices = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
            foreach (var d in hostedServices)
                services.Remove(d);

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.ConfigurationManager = null;
                options.TokenValidationParameters.ConfigurationManager = null;
                options.TokenValidationParameters.ValidIssuer = TestJwtTokenFactory.KeycloakIssuer;
                options.TokenValidationParameters.IssuerSigningKey = new RsaSecurityKey(TestJwtKeys.CreateRsa())
                {
                    KeyId = TestJwtKeys.Kid
                };
            });
        });
    }

    public async Task MigrateAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task CleanAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE ledger.outbox_messages, ledger.idempotency_records, ledger.estornos_lancamentos, ledger.ledger_entries RESTART IDENTITY;");
    }
}
