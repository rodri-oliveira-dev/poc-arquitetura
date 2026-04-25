using FluentAssertions;
using System.Xml.Linq;

namespace LedgerService.UnitTests.Tests;

public sealed class DependencyPolicyTests
{
    private const string CryptographyXmlPackage = "System.Security.Cryptography.Xml";
    private const string OpenTelemetryApiPackage = "OpenTelemetry.Api";

    [Fact]
    public void Vulnerable_cryptography_xml_override_should_be_centralized()
    {
        var repositoryRoot = GetRepositoryRoot();
        var packages = XDocument.Load(Path.Combine(repositoryRoot.FullName, "Directory.Packages.props"));

        var packageVersion = packages
            .Descendants("PackageVersion")
            .SingleOrDefault(element => (string?)element.Attribute("Include") == CryptographyXmlPackage);

        packageVersion.Should().NotBeNull();
        packageVersion!.Attribute("Version")!.Value.Should().Be("10.0.7");
    }

    [Fact]
    public void Vulnerable_opentelemetry_api_override_should_use_fixed_version()
    {
        var repositoryRoot = GetRepositoryRoot();
        var packages = XDocument.Load(Path.Combine(repositoryRoot.FullName, "Directory.Packages.props"));

        var packageVersion = packages
            .Descendants("PackageVersion")
            .SingleOrDefault(element => (string?)element.Attribute("Include") == OpenTelemetryApiPackage);

        packageVersion.Should().NotBeNull();
        packageVersion!.Attribute("Version")!.Value.Should().Be("1.15.3");
    }

    [Fact]
    public void Dotnet_ci_should_block_moderate_or_higher_nuget_vulnerabilities()
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/dotnet.yml"));

        workflow.Should().Contain("""blocked_severities = {"moderate", "high", "critical"}""");
    }

    [Fact]
    public void Dependency_review_should_block_moderate_or_higher_vulnerabilities()
    {
        var repositoryRoot = GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot.FullName, ".github/workflows/dependency-review.yml"));

        workflow.Should().Contain("fail-on-severity: moderate");
    }

    [Theory]
    [InlineData("src/LedgerService.Infrastructure/LedgerService.Infrastructure.csproj")]
    [InlineData("src/BalanceService.Infrastructure/BalanceService.Infrastructure.csproj")]
    public void Infrastructure_projects_should_reference_cryptography_xml_without_local_version(string projectPath)
    {
        var repositoryRoot = GetRepositoryRoot();
        var project = XDocument.Load(Path.Combine(repositoryRoot.FullName, projectPath));

        var packageReference = project
            .Descendants("PackageReference")
            .SingleOrDefault(element => (string?)element.Attribute("Include") == CryptographyXmlPackage);

        packageReference.Should().NotBeNull();
        packageReference!.Attribute("Version").Should().BeNull();
        packageReference.Attribute("PrivateAssets")!.Value.Should().Be("all");
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
