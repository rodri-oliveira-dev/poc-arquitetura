using IdentityService.Application.Users.Ports;
using IdentityService.Infrastructure.Persistence;
using IdentityService.IntegrationTests.Infrastructure.Security;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.IntegrationTests.Infrastructure;

public sealed class PostgresIdentityApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Test");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddDebug();
            logging.AddProvider(new RecordingLoggerProvider(LogMessages));
        });

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = TestJwtTokenFactory.KeycloakIssuer,
                ["Jwt:Audience"] = TestJwtTokenFactory.IdentityAudience,
                ["Jwt:JwksUrl"] = "https://localhost/jwks.json",
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Email:Provider"] = "Mailpit",
                ["Email:AuthenticationUrl"] = "https://auth.localhost/login"
            });
        });

        builder.ConfigureServices(services =>
        {
            RemoveService<IdentityDbContext>(services);
            RemoveService<DbContextOptions<IdentityDbContext>>(services);
            RemoveAll<IIdentityProviderUserService>(services);
            RemoveAll<IEmailSender>(services);

            services.AddDbContext<IdentityDbContext>(options =>
                options.UseNpgsql(
                    connectionString,
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));

            services.AddSingleton<IIdentityProviderUserService>(IdentityProvider);
            services.AddSingleton<IEmailSender>(EmailSender);

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

    public RecordingIdentityProviderUserService IdentityProvider
    {
        get;
    } = new();

    public RecordingEmailSender EmailSender
    {
        get;
    } = new();

    public List<string> LogMessages
    {
        get;
    } = [];

    private static void RemoveService<TService>(IServiceCollection services)
    {
        ServiceDescriptor? descriptor = services.SingleOrDefault(descriptor => descriptor.ServiceType == typeof(TService));
        if (descriptor is not null)
        {
            services.Remove(descriptor);
        }
    }

    private static void RemoveAll<TService>(IServiceCollection services)
    {
        foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(TService)).ToArray())
        {
            services.Remove(descriptor);
        }
    }

    public sealed class RecordingIdentityProviderUserService : IIdentityProviderUserService
    {
        private readonly Lock _sync = new();

        public List<CreateIdentityProviderUserRequest> CreateRequests
        {
            get;
        } = [];

        public List<string> DeletedUserIds
        {
            get;
        } = [];

        public TaskCompletionSource<bool>? CreateEntered
        {
            get;
            set;
        }

        public TaskCompletionSource<bool>? ReleaseCreate
        {
            get;
            set;
        }

        public Task<CreateIdentityProviderUserResult> CreateUserAsync(
            CreateIdentityProviderUserRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            return CreateUserCoreAsync(request, cancellationToken);
        }

        public Task DeleteUserAsync(string keycloakUserId, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                DeletedUserIds.Add(keycloakUserId);
            }

            return Task.CompletedTask;
        }

        private async Task<CreateIdentityProviderUserResult> CreateUserCoreAsync(
            CreateIdentityProviderUserRequest request,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                CreateRequests.Add(request);
            }

            CreateEntered?.TrySetResult(true);

            if (ReleaseCreate is not null)
                await ReleaseCreate.Task.WaitAsync(cancellationToken);

            return new CreateIdentityProviderUserResult($"kc-{Guid.NewGuid():N}");
        }
    }

    public sealed class RecordingEmailSender : IEmailSender
    {
        private readonly Lock _sync = new();

        public List<EmailMessage> Messages
        {
            get;
        } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                Messages.Add(message);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLoggerProvider(List<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(messages, categoryName);

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(List<string> messages, string categoryName) : ILogger
    {
        private readonly Lock _sync = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

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

            lock (_sync)
            {
                messages.Add($"{categoryName}: {formatter(state, exception)} {exception}");
            }
        }
    }
}
