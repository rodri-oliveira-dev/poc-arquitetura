using FluentAssertions;

namespace LedgerService.UnitTests.Tests;

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

        dockerfile.Should().Contain("COPY --from=build --chown=$APP_UID:0 /app/publish ./");
        dockerfile.Should().Contain("USER $APP_UID");
    }

    [Fact]
    public void Auth_api_dockerfile_should_prepare_writable_data_directory_for_non_root_user()
    {
        var repositoryRoot = GetRepositoryRoot();
        var dockerfile = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "src/Auth.Api/Dockerfile"));

        dockerfile.Should().Contain("mkdir -p /data");
        dockerfile.Should().Contain("chown -R $APP_UID:0 /app /data");
    }

    [Fact]
    public void Compose_should_not_use_latest_image_tags()
    {
        var repositoryRoot = GetRepositoryRoot();

        foreach (var composePath in new[] { "compose.yaml", "compose.k6.yaml" })
        {
            var compose = File.ReadAllText(Path.Combine(repositoryRoot.FullName, composePath));

            compose.Should().NotContain(":latest");
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

            serviceBlock.Should().Contain("deploy:");
            serviceBlock.Should().Contain("resources:");
            serviceBlock.Should().Contain("limits:");
            serviceBlock.Should().Contain("cpus:");
            serviceBlock.Should().Contain("memory:");
            serviceBlock.Should().Contain("pids:");
        }
    }

    [Fact]
    public void K6_compose_service_should_define_local_resource_limits()
    {
        var repositoryRoot = GetRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(repositoryRoot.FullName, "compose.k6.yaml"));
        var serviceBlock = GetServiceBlock(compose, "k6");

        serviceBlock.Should().Contain("deploy:");
        serviceBlock.Should().Contain("resources:");
        serviceBlock.Should().Contain("limits:");
        serviceBlock.Should().Contain("cpus:");
        serviceBlock.Should().Contain("memory:");
        serviceBlock.Should().Contain("pids:");
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

        start.Should().BeGreaterThanOrEqualTo(0, $"service {serviceName} must exist in compose");

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

        directory.Should().NotBeNull("the test must run inside the repository tree");
        return directory!;
    }
}
