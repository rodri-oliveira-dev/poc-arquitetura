
namespace LedgerService.UnitTests.Architecture;

public sealed class ContainerSecurityPolicyTests
{
    private static readonly string[] ApiDockerfiles =
    [
        "src/Auth.Api/Dockerfile",
        "src/LedgerService.Api/Dockerfile",
        "src/BalanceService.Api/Dockerfile",
    ];

    private static readonly string[] ComposeServicesWithLimits =
    [
        "ledger-db",
        "balance-db",
        "kafka",
        "kafka-init-topics",
        "ledger-service",
        "balance-service",
        "auth-api",
    ];

    [Theory]
    [MemberData(nameof(ApiDockerfilePaths))]
    public void Api_dockerfiles_should_run_final_stage_as_non_root(string dockerfilePath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var dockerfile = File.ReadAllText(Path.Combine(repositoryRoot.FullName, dockerfilePath));
        Assert.Contains("COPY --from=build --chown=$APP_UID:0 /app/publish ./", dockerfile);
        Assert.Contains("USER $APP_UID", dockerfile);
    }

    [Fact]
    public void Auth_api_dockerfile_should_prepare_writable_data_directory_for_non_root_user()
    {
        var repositoryRoot = GetRepositoryRoot();
        var dockerfile = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "src/Auth.Api/Dockerfile"));
        Assert.Contains("mkdir -p /data", dockerfile);
        Assert.Contains("chown -R $APP_UID:0 /app /data", dockerfile);
    }

    [Fact]
    public void Compose_should_not_use_latest_image_tags()
    {
        var repositoryRoot = GetRepositoryRoot();

        foreach (var composePath in new[] { "compose.yaml", "compose.k6.yaml" })
        {
            var compose = File.ReadAllText(Path.Combine(repositoryRoot.FullName, composePath));
        Assert.DoesNotContain(":latest", compose);
        }
    }

    [Fact]
    public void Compose_services_should_define_local_resource_limits()
    {
        var repositoryRoot = GetRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "compose.yaml"));

        foreach (var service in ComposeServicesWithLimits)
        {
            var serviceBlock = GetServiceBlock(compose, service);
        Assert.Contains("deploy:", serviceBlock);
        Assert.Contains("resources:", serviceBlock);
        Assert.Contains("limits:", serviceBlock);
        Assert.Contains("cpus:", serviceBlock);
        Assert.Contains("memory:", serviceBlock);
        Assert.Contains("pids:", serviceBlock);
        }
    }

    [Fact]
    public void K6_compose_service_should_define_local_resource_limits()
    {
        var repositoryRoot = GetRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "compose.k6.yaml"));
        var serviceBlock = GetServiceBlock(compose, "k6");
        Assert.Contains("deploy:", serviceBlock);
        Assert.Contains("resources:", serviceBlock);
        Assert.Contains("limits:", serviceBlock);
        Assert.Contains("cpus:", serviceBlock);
        Assert.Contains("memory:", serviceBlock);
        Assert.Contains("pids:", serviceBlock);
    }

    public static TheoryData<string> ApiDockerfilePaths()
    {
        var data = new TheoryData<string>();

        foreach (var dockerfile in ApiDockerfiles)
            data.Add(dockerfile);

        return data;
    }

    private static string GetServiceBlock(string compose, string serviceName)
    {
        var lines = compose.Split('\n');
        var marker = $"  {serviceName}:";
        var start = Array.FindIndex(lines, line => line.TrimEnd('\r') == marker);
        Assert.True(start >= 0);
        var end = Array.FindIndex(lines, start + 1, line =>
        {
            var normalized = line.TrimEnd('\r');
            return normalized.StartsWith("  ", StringComparison.Ordinal)
                && !normalized.StartsWith("    ", StringComparison.Ordinal)
                && normalized.EndsWith(':');
        });

        if (end < 0)
            end = lines.Length;

        return string.Join('\n', lines[start..end]);
    }

    private static DirectoryInfo GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LedgerService.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory!;
    }
}
