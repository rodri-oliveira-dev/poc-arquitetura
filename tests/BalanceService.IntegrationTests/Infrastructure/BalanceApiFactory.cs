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
            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<BalanceDbContext>));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            // Nome único por factory para reduzir interferência entre testes.
            services.AddDbContext<BalanceDbContext>(options => options.UseInMemoryDatabase(_dbName));

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
