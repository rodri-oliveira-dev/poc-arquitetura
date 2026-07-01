using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

using TransferService.Infrastructure.Persistence;
using TransferService.IntegrationTests.Infrastructure.Security;

namespace TransferService.IntegrationTests.Infrastructure;

public sealed class TransferApiFactory : WebApplicationFactory<Program>, IDisposable
{
    private static readonly IServiceProvider InMemoryEfProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    private readonly Dictionary<string, string?> _previousEnvironmentValues = new(StringComparer.Ordinal);
    private readonly string _dbName = $"transfer-it-{Guid.NewGuid():N}";
    private readonly InMemoryDatabaseRoot _databaseRoot = new();

    public IReadOnlyCollection<string> LogMessages => _loggerProvider.Messages;

    private readonly TestLoggerProvider _loggerProvider = new();

    public TransferApiFactory()
    {
        SetProcessEnvironmentDefault("Jwt__Issuer", TestJwtTokenFactory.KeycloakIssuer);
        SetProcessEnvironmentDefault("Jwt__Audience", TestJwtTokenFactory.TransferAudience);
        SetProcessEnvironmentDefault("Jwt__JwksUrl", "https://localhost/jwks.json");
        SetProcessEnvironmentDefault("ApiLimits__MaxRequestBodySizeBytes", "1024");
        SetProcessEnvironmentDefault("ConnectionStrings__DefaultConnection", "Host=unused;Database=ignore;Username=ignore;Password=ignore");
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
            logging.AddProvider(_loggerProvider);
        });

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = TestJwtTokenFactory.KeycloakIssuer,
                ["Jwt:Audience"] = TestJwtTokenFactory.TransferAudience,
                ["Jwt:JwksUrl"] = "https://localhost/jwks.json",
                ["ApiLimits:MaxRequestBodySizeBytes"] = "1024",
                ["ConnectionStrings:DefaultConnection"] = "Host=unused;Database=ignore;Username=ignore;Password=ignore"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TransferServiceDbContext>();
            services.RemoveAll<DbContextOptions<TransferServiceDbContext>>();

            var npgsqlProviderDescriptors = services
                .Where(d =>
                    (d.ServiceType.Assembly.GetName().Name?.StartsWith("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.OrdinalIgnoreCase) ?? false)
                    || (d.ImplementationType?.Assembly.GetName().Name?.StartsWith("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.OrdinalIgnoreCase) ?? false)
                    || (d.ImplementationInstance?.GetType().Assembly.GetName().Name?.StartsWith("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();

            foreach (var d in npgsqlProviderDescriptors)
                services.Remove(d);

            services.AddDbContext<TransferServiceDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName, _databaseRoot);
                options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                options.UseInternalServiceProvider(InMemoryEfProvider);
            });

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

    public TransferServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TransferServiceDbContext>()
            .UseInMemoryDatabase(_dbName, _databaseRoot)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new TransferServiceDbContext(options);
    }

    public DbContextOptionsBuilder ConfigureDbContext(DbContextOptionsBuilder options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.UseInMemoryDatabase(_dbName, _databaseRoot);
        options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        return options;
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

    private sealed class TestLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages = [];

        public IReadOnlyCollection<string> Messages => _messages;

        public ILogger CreateLogger(string categoryName) => new TestLogger(categoryName, _messages);

        public void Dispose()
        {
        }

        private sealed class TestLogger(string categoryName, List<string> messages) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull
                => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                    return;

                var message = $"{logLevel}: {categoryName}: {formatter(state, exception)}";
                if (exception is not null)
                    message += Environment.NewLine + exception;

                lock (messages)
                {
                    messages.Add(message);
                }
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
