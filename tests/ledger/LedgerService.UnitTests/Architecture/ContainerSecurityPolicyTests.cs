
namespace LedgerService.UnitTests.Architecture;

public sealed class ContainerSecurityPolicyTests
{
    [Fact]
    public void Dockerfiles_and_compose_should_follow_container_baseline()
    {
        var repositoryRoot = GetRepositoryRoot();

        var failures = ContainerBaselineValidator.Program.Validate(repositoryRoot.FullName);

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static DirectoryInfo GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PocArquitetura.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory;
    }
}
