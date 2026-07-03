using System.Diagnostics.CodeAnalysis;

using DotNet.Testcontainers.Configurations;

using Testcontainers.PostgreSql;

namespace LedgerService.IntegrationTests.Infrastructure;

public sealed class PostgresLedgerFixture : IAsyncLifetime
{
    private const string PostgresImage = "docker.io/postgres:16";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(PostgresImage)
        .WithDatabase("appdb")
        .WithUsername("appuser")
        .WithPassword("app123")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        WriteDiagnostic("Starting PostgreSQL Testcontainer.");

        try
        {
            await _postgres.StartAsync();
            WriteDiagnostic($"PostgreSQL Testcontainer started. Container={_postgres.Name}; ConnectionString={RedactPassword(ConnectionString)}");

            await using var factory = new PostgresLedgerApiFactory(ConnectionString);
            await factory.MigrateAsync();
        }
        catch (Exception ex)
        {
            WriteDiagnostic($"PostgreSQL Testcontainer initialization failed.{Environment.NewLine}{ex}");
            await TryWriteContainerLogsAsync();
            throw;
        }
    }

    public async Task CleanAsync()
    {
        await using var factory = new PostgresLedgerApiFactory(ConnectionString);
        await factory.CleanAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort diagnostic path must not hide the original Testcontainers failure.")]
    private async Task TryWriteContainerLogsAsync()
    {
        try
        {
            var (stdout, stderr) = await _postgres.GetLogsAsync(
                since: DateTime.MinValue,
                until: DateTime.UtcNow,
                timestampsEnabled: true,
                ct: TestContext.Current.CancellationToken);

            WriteDiagnostic(
                $"PostgreSQL Testcontainer logs.{Environment.NewLine}" +
                $"stdout:{Environment.NewLine}{stdout}{Environment.NewLine}" +
                $"stderr:{Environment.NewLine}{stderr}");
        }
        catch (Exception logsException)
        {
            WriteDiagnostic($"PostgreSQL Testcontainer logs unavailable. {logsException}");
        }
    }

    private static void WriteDiagnostic(string message)
    {
        Console.Error.WriteLine(
            $"[LedgerService.IntegrationTests][PostgresLedgerFixture] " +
            $"Image={PostgresImage}; Docker={GetDockerEndpointDescription()}; {message}");
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort diagnostic path must not hide the original Testcontainers failure.")]
    private static string GetDockerEndpointDescription()
    {
        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
        var dockerHostDescription = string.IsNullOrWhiteSpace(dockerHost)
            ? "DOCKER_HOST=<empty>"
            : $"DOCKER_HOST={dockerHost}";

        try
        {
            return $"{dockerHostDescription}; ResolvedEndpoint={TestcontainersSettings.OS.DockerEndpointAuthConfig.Endpoint}";
        }
        catch (Exception ex)
        {
            return $"{dockerHostDescription}; ResolvedEndpoint=<unavailable: {ex.GetType().Name}: {ex.Message}>";
        }
    }

    private static string RedactPassword(string connectionString)
    {
        return string.Join(
            ';',
            connectionString
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.StartsWith("Password=", StringComparison.OrdinalIgnoreCase)
                    ? "Password=<redacted>"
                    : part));
    }
}
