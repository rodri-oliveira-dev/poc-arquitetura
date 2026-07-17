using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

using PaymentService.Infrastructure.Persistence;
using PaymentService.IntegrationTests.Infrastructure.Security;

namespace PaymentService.IntegrationTests.Infrastructure;

public sealed class PostgresPaymentApiFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly Dictionary<string, string?> _previousEnvironmentValues = new(StringComparer.Ordinal);
    private readonly string _connectionString;
    private readonly Action<IServiceCollection>? _configureTestServices;

    public PostgresPaymentApiFactory(
        string connectionString,
        Action<IServiceCollection>? configureTestServices = null)
    {
        _connectionString = connectionString;
        _configureTestServices = configureTestServices;
        SetProcessEnvironmentDefault("Jwt__Issuer", TestJwtTokenFactory.KeycloakIssuer);
        SetProcessEnvironmentDefault("Jwt__Audience", TestJwtTokenFactory.PaymentAudience);
        SetProcessEnvironmentDefault("Jwt__JwksUrl", "https://localhost/jwks.json");
        SetProcessEnvironmentDefault("ApiLimits__MaxRequestBodySizeBytes", "1024");
        SetProcessEnvironmentDefault("ConnectionStrings__DefaultConnection", connectionString);
        SetProcessEnvironmentDefault("ForwardedHeaders__TrustedProxies__0", "127.0.0.1");
        SetProcessEnvironmentDefault("ForwardedHeaders__AllowedHosts__0", "api.example.com");
        SetProcessEnvironmentDefault("PaymentGateway__Stripe__WebhookSigningSecret", StripeWebhookTestData.Secret);
        SetProcessEnvironmentDefault("PaymentGateway__Stripe__WebhookSignatureTolerance", "00:05:00");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Test");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddDebug();
            logging.AddConsole();
        });

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = TestJwtTokenFactory.KeycloakIssuer,
                ["Jwt:Audience"] = TestJwtTokenFactory.PaymentAudience,
                ["Jwt:JwksUrl"] = "https://localhost/jwks.json",
                ["ApiLimits:MaxRequestBodySizeBytes"] = "1024",
                ["ForwardedHeaders:TrustedProxies:0"] = "127.0.0.1",
                ["ForwardedHeaders:AllowedHosts:0"] = "api.example.com",
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["PaymentGateway:Stripe:WebhookSigningSecret"] = StripeWebhookTestData.Secret,
                ["PaymentGateway:Stripe:WebhookSignatureTolerance"] = "00:05:00"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<PaymentDbContext>();
            services.RemoveAll<DbContextOptions<PaymentDbContext>>();

            services.AddDbContext<PaymentDbContext>(options =>
                options.UseNpgsql(
                    _connectionString,
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "payment")));

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

            _configureTestServices?.Invoke(services);
        });
    }

    public new void Dispose()
    {
        foreach (var (name, value) in _previousEnvironmentValues)
            Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);

        base.Dispose();
    }

    private void SetProcessEnvironmentDefault(string name, string value)
    {
        var previous = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        _previousEnvironmentValues[name] = previous;

        if (string.IsNullOrWhiteSpace(previous))
            Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
    }
}
