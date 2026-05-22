
namespace LedgerService.UnitTests.Architecture;

public sealed class ComposeEnvGenYamlTests
{
    [Fact]
    public void Compose_env_gen_should_read_current_compose_yaml_and_generate_k6_environment_file()
    {
        var repositoryRoot = GetRepositoryRoot();
        var composePath = Path.Combine(repositoryRoot.FullName, "compose.yaml");
        var outputPath = Path.Combine(Path.GetTempPath(), $"compose-env-{Guid.NewGuid():N}.env");

        try
        {
            var exitCode = ComposeEnvGen.Program.Main(["--compose", composePath, "--out", outputPath]);
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath));
            var generatedEnvironment = File.ReadAllLines(outputPath);
        Assert.Contains("LEDGER_SERVICE_NAME=ledger-service", generatedEnvironment);
        Assert.Contains("BALANCE_SERVICE_NAME=balance-service", generatedEnvironment);
        Assert.Contains("AUTH_SERVICE_NAME=auth-api", generatedEnvironment);
        Assert.Contains("BASE_URL_LEDGER=http://ledger-service:8080", generatedEnvironment);
        Assert.Contains("BASE_URL_BALANCE=http://balance-service:8080", generatedEnvironment);
        Assert.Contains("AUTH_BASE_URL=http://auth-api:8080", generatedEnvironment);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
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
